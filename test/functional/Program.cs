using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace Splunk.Logging.FunctionalTest
{
    class SplunkCliWrapper
    {
        private string splunkCmd = string.Empty;
        private string userName = string.Empty, password = string.Empty;

        public void DeleteIndex(string indexName)
        {
            string stdOut, stdError;
            ExecuteSplunkCli(string.Format(CultureInfo.InvariantCulture, "remove index {0}", indexName), out stdOut, out stdError);
        }

        public void CreateIndex(string indexName)
        {
            string stdOut, stdError;
            if (!ExecuteSplunkCli(string.Format(CultureInfo.InvariantCulture, "add index {0}", indexName), out stdOut, out stdError))
            {
                Console.WriteLine("Failed to create index. {1} {2}", stdOut, stdError);
                Environment.Exit(2);
            }
        }

        public int GetSearchCount(string searchQuery)
        {
            string stdOut, stdError;
            if (!ExecuteSplunkCli(string.Format(CultureInfo.InvariantCulture, "search \"{0} | stats count\" -preview false", searchQuery), out stdOut, out stdError))
            {
                Console.WriteLine("Failed to run search query '{0}'. {1} {2}", searchQuery, stdOut, stdError);
                Environment.Exit(2);
            }
            return Convert.ToInt32(stdOut.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[2], CultureInfo.InvariantCulture);
        }

        public List<string> GetSearchResults(string searchQuery)
        {
            string stdOut, stdError;
            if (!ExecuteSplunkCli(string.Format(CultureInfo.InvariantCulture, "search \"{0}\" -preview false -maxout 0", searchQuery), out stdOut, out stdError))
            {
                Console.WriteLine("Failed to run search query '{0}'. {1} {2}", searchQuery, stdOut, stdError);
                Environment.Exit(2);
            }
            return new List<string>(stdOut.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        }

        public List<string> GetMetadataResults(string searchQuery)
        {
            string stdOut, stdError;
            if (!ExecuteSplunkCli(string.Format(CultureInfo.InvariantCulture, "search \"{0} | stats count by host, sourcetype, source\" -preview false", searchQuery), out stdOut, out stdError))
            {
                Console.WriteLine("Failed to run search query '{0}'. {1} {2}", searchQuery, stdOut, stdError);
                Environment.Exit(2);
            }
            string metaData = stdOut.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[2];
            return new List<string>(metaData.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        }

        public static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        public static double GetEpochTime()
        {
            return (DateTime.UtcNow - UnixEpoch).TotalSeconds;
        }

        public void WaitForIndexingToComplete(string indexName, double startTime=0)
        {
            string query = string.Format(CultureInfo.InvariantCulture, "index={0} earliest={1:F2}", indexName, startTime);
            int eventCount = GetSearchCount(query);
            for (int i = 0; i < 10; i++) // Exit only if there were no indexing activities for 10 straight seconds
            {
                do
                {
                    Thread.Sleep(1000);
                    int updatedEventCount = GetSearchCount(query);
                    if (updatedEventCount == eventCount)
                        break;
                    eventCount = updatedEventCount;
                    i = 0; // Indexing is still goes on, reset waiting 
                } while (true);
            }
        }

        private bool ExecuteSplunkCli(string command, out string stdOut, out string stdError)
        {
            Process splunkProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = this.splunkCmd,
                    Arguments = string.Format(CultureInfo.InvariantCulture, "{0} -auth {1}:{2}", command, userName, password),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            splunkProcess.Start();
            stdOut = splunkProcess.StandardOutput.ReadToEnd();
            stdError = splunkProcess.StandardError.ReadToEnd();
            if (splunkProcess.ExitCode != 0)
            {
                return false;
            }
            return true;
        }

        private bool ExecuteSplunkCliHttpInput(string command, out string stdOut, out string stdError)
        {
            return ExecuteSplunkCli(string.Format(CultureInfo.InvariantCulture, "http-input {0} -uri https://127.0.0.1:8089", command), out stdOut, out stdError);
        }

        public void EnableHttp()
        {
            string stdOut, stdError;
            if (!ExecuteSplunkCliHttpInput("enable", out stdOut, out stdError))
            {
                Console.WriteLine("Failed to execute 'enable' command. {0} {1}", stdOut, stdError);
                Environment.Exit(2);
            }
        }

        public void DeleteToken(string tokenName)
        {
            string stdOut, stdError;
            ExecuteSplunkCliHttpInput(string.Format(CultureInfo.InvariantCulture, "delete -name {0}", tokenName), out stdOut, out stdError);
        }

        public string CreateToken(string tokenName, string indexes = null, string index = null)
        {
            string stdOut, stdError;
            string createCmd = string.Format(CultureInfo.InvariantCulture, "create -name {0}", tokenName);
            if (!string.IsNullOrEmpty(indexes))
                createCmd += string.Format(CultureInfo.InvariantCulture, " -indexes {0}", indexes);
            if (!string.IsNullOrEmpty(index))
                createCmd += string.Format(CultureInfo.InvariantCulture, " -index {0}", index);
            if (!ExecuteSplunkCliHttpInput(createCmd, out stdOut, out stdError))
            {
                Console.WriteLine("Failed to create token. {1} {2}", stdOut, stdError);
                Environment.Exit(2);
            }
            int idx1 = stdOut.IndexOf("token=", StringComparison.Ordinal) + 6, idx2 = idx1;
            while (stdOut[idx2] != '\r')
                idx2++;
            string result = stdOut.Substring(idx1, idx2 - idx1);
            return result;
        }

        public SplunkCliWrapper(string userName = "admin", string password = "changeme")
        {
            this.userName = userName;
            this.password = password;

            // Get splunkd location
            Process serviceQuery = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = "qc splunkd",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            serviceQuery.Start();
            string output = serviceQuery.StandardOutput.ReadToEnd();
            if (serviceQuery.ExitCode != 0)
            {
                Console.WriteLine("Failed to execute query command, exit code {0}. Output is {1}", serviceQuery.ExitCode, output);
                Environment.Exit(1);
            }
            int idx1 = output.IndexOf("BINARY_PATH_NAME", StringComparison.Ordinal);
            idx1 = output.IndexOf(":", idx1 + 1,StringComparison.Ordinal) + 1;
            while (output[idx1] == ' ')
                idx1++;
            int idx2 = output.IndexOf("service", idx1, StringComparison.Ordinal) - 1;
            while (output[idx2] == ' ')
                idx2--;
            this.splunkCmd = output.Substring(idx1, idx2 - idx1 + 1).Replace("splunkd.exe", "splunk.exe");
        }
    }

    class Program
    {
        private static bool GenerateDataWaitForIndexingCompletion(SplunkCliWrapper splunk, string indexName, double testStartTime, TraceSource trace)
        {
            // Generate data
            int eventCounter = GenerateData(trace);
            string searchQuery = "index=" + indexName;
            Console.WriteLine("{0} events were created, waiting for indexing to complete.", eventCounter);
            splunk.WaitForIndexingToComplete(indexName);
            int eventsFound = splunk.GetSearchCount(searchQuery);
            Console.WriteLine("Indexing completed, {0} events were found. Elapsed time {1:F2} seconds", eventsFound, SplunkCliWrapper.GetEpochTime() - testStartTime);
            // Verify all events were indexed correctly
            if(eventCounter != eventsFound)
            {
                Console.WriteLine("Test FAILED.");
                Console.WriteLine();
                return false;
            }
            List<string> searchResults = splunk.GetSearchResults(searchQuery);
            if (searchResults.Count != eventsFound)
            {
                Console.WriteLine("Search query returned {0} events. Test FAILED.", searchResults.Count);
                Console.WriteLine();
                return false;
            }
            for (int eventId = 0; eventId < eventCounter; eventId++)
            {
                string expected = string.Format("This is event {0}", eventId);
                bool found = false;
                foreach(string s in searchResults)
                {
                    if(s.Contains(expected))
                    {
                        found = true;
                        break;
                    }
                }
                if(!found)
                {
                    Console.WriteLine("Failed to find '{0}' in search results. Test FAILED.", expected);
                    Console.WriteLine();
                    return false;
                }
            }
            // Verify metadata
            List<string> metaData = splunk.GetMetadataResults(searchQuery);
            if(metaData[0] != "customhostname")
            {
                Console.WriteLine("Wrong metadata. Host name is '{0}' instead of 'customehostname'. Test FAILED.", metaData[0]);
                Console.WriteLine();
                return false;
            }
            if (metaData[1] != "log")
            {
                Console.WriteLine("Wrong metadata. Sourcetype is '{0}' instead of 'log'. Test FAILED.", metaData[1]);
                Console.WriteLine();
                return false;
            }
            if (metaData[2] != "host")
            {
                Console.WriteLine("Wrong metadata. Source is is '{0}' instead of 'host'. Test FAILED.", metaData[2]);
                Console.WriteLine();
                return false;
            }
            Console.WriteLine("Test PASSED.");
            Console.WriteLine();
            return true;
        }

        private static int GenerateData(TraceSource trace)
        {
            int eventCounter = 0, id = 0;
            const int eventsPerLoop = 50;
            foreach (TraceEventType eventType in new TraceEventType[] { TraceEventType.Error, TraceEventType.Information, TraceEventType.Warning })
            {
                for (int i = 0; i < eventsPerLoop; i++, id++, eventCounter++)
                {
                    trace.TraceData(eventType, id, new string[] { "TraceData", eventType.ToString(), string.Format("This is event {0}", id) });
                }
            }
            foreach (TraceEventType eventType in new TraceEventType[] { TraceEventType.Error, TraceEventType.Information, TraceEventType.Warning })
            {
                for (int i = 0; i < eventsPerLoop; i++, id++, eventCounter++)
                {
                    trace.TraceEvent(eventType, id, "TraceEvent " + eventType.ToString() + string.Format(" This is event {0}", id));
                }
            }
            for (int i = 0; i < eventsPerLoop; i++, id++, eventCounter++)
            {
                trace.TraceInformation(string.Format("TraceInformation. This is event {0}", id));
            }
            return eventCounter;
        }

        private static string CreateIndexAndToken(SplunkCliWrapper splunk, string tokenName, string indexName)
        {
            splunk.EnableHttp();
            splunk.DeleteToken(tokenName);
            string token = splunk.CreateToken(tokenName, indexes: indexName, index: indexName);
            splunk.DeleteIndex(indexName);
            splunk.CreateIndex(indexName);
            return token;
        }

        private static void EnableSelfSignedCertificates()
        {
            // Enable self signed certificates
            System.Net.ServicePointManager.ServerCertificateValidationCallback +=
                delegate(object sender, System.Security.Cryptography.X509Certificates.X509Certificate certificate,
                    System.Security.Cryptography.X509Certificates.X509Chain chain,
                    System.Net.Security.SslPolicyErrors sslPolicyErrors)
                {
                    return true;
                };
        }

        static bool SendEventsBatchedByTime()
        {
            string tokenName = "batchedbytimetoken";
            string indexName = "batchedbytimeindex";
            SplunkCliWrapper splunk = new SplunkCliWrapper();
            double testStartTime = SplunkCliWrapper.GetEpochTime();
            string token = CreateIndexAndToken(splunk, tokenName, indexName);
            Console.WriteLine("Test SendEventsBatchedByTime started.");

            var trace = new TraceSource("HttpInputLogger");
            trace.Switch.Level = SourceLevels.All;
            var meta = new Dictionary<string, string>();
            meta["index"] = indexName;
            meta["source"] = "host";
            meta["sourcetype"] = "log";
            meta["host"] = "customhostname";
            var listener = new HttpInputTraceListener(
                uri: new Uri("https://127.0.0.1:8089"),
                token: token,
                metadata: meta,
                batchSizeBytes:100000,
                batchInterval: 1000);
            trace.Listeners.Add(listener);

            return GenerateDataWaitForIndexingCompletion(splunk, indexName, testStartTime, trace);
        }

        static bool SendEventsBatchedBySize()
        {
            string tokenName = "batchedbysizetoken";
            string indexName = "batchedbysizeindex";
            SplunkCliWrapper splunk = new SplunkCliWrapper();
            double testStartTime = SplunkCliWrapper.GetEpochTime();
            string token = CreateIndexAndToken(splunk, tokenName, indexName);
            Console.WriteLine("Test SendEventsBatchedBySize started.");

            var trace = new TraceSource("HttpInputLogger");
            trace.Switch.Level = SourceLevels.All;
            var meta = new Dictionary<string, string>();
            meta["index"] = indexName;
            meta["source"] = "host";
            meta["sourcetype"] = "log";
            meta["host"] = "customhostname";
            var listener = new HttpInputTraceListener(
                uri: new Uri("https://127.0.0.1:8089"),
                token: token,
                metadata: meta,
                batchSizeCount: 50);
            trace.Listeners.Add(listener);

            return GenerateDataWaitForIndexingCompletion(splunk, indexName, testStartTime, trace);
        }

        static bool SendEventsBatchedBySizeAndTime()
        {
            string tokenName = "batchedbysizeandtimetoken";
            string indexName = "batchedbysizeandtimeindex";
            SplunkCliWrapper splunk = new SplunkCliWrapper();
            double testStartTime = SplunkCliWrapper.GetEpochTime();
            string token = CreateIndexAndToken(splunk, tokenName, indexName);
            Console.WriteLine("Test SendEventsBatchedBySizeAndTime started.");

            var trace = new TraceSource("HttpInputLogger");
            trace.Switch.Level = SourceLevels.All;
            var meta = new Dictionary<string, string>();
            meta["index"] = indexName;
            meta["source"] = "host";
            meta["sourcetype"] = "log";
            meta["host"] = "customhostname";
            var listener = new HttpInputTraceListener(
                uri: new Uri("https://127.0.0.1:8089"),
                token: token,
                metadata: meta,
                batchSizeCount: 60, batchInterval:2000);
            trace.Listeners.Add(listener);

            return GenerateDataWaitForIndexingCompletion(splunk, indexName, testStartTime, trace);
        }

        static bool SendEventsUnBatched()
        {
            string tokenName = "unbatchedtoken";
            string indexName = "unbatchedindex";
            SplunkCliWrapper splunk = new SplunkCliWrapper();
            double testStartTime = SplunkCliWrapper.GetEpochTime();
            string token = CreateIndexAndToken(splunk, tokenName, indexName);
            Console.WriteLine("Test SendEventsUnBatched started.");

            var trace = new TraceSource("HttpInputLogger");
            trace.Switch.Level = SourceLevels.All;
            var meta = new Dictionary<string, string>();
            meta["index"] = indexName;
            meta["source"] = "host";
            meta["sourcetype"] = "log";
            meta["host"] = "customhostname";
            var listener = new HttpInputTraceListener(
                uri: new Uri("https://127.0.0.1:8089"),
                token: token,
                metadata: meta);
            trace.Listeners.Add(listener);

            return GenerateDataWaitForIndexingCompletion(splunk, indexName, testStartTime, trace);
        }

        static bool VerifyErrorsAreRaised()
        {
            string indexName = "errorindex";
            string tokenName = "errortoken";
            double testStartTime = SplunkCliWrapper.GetEpochTime();
            SplunkCliWrapper splunk = new SplunkCliWrapper();
            string token = CreateIndexAndToken(splunk, tokenName, indexName);
            Console.WriteLine("Test VerifyErrorsAreRaised started.");

            var trace = new TraceSource("HttpInputLogger");
            trace.Switch.Level = SourceLevels.All;

            var validMetaData = new Dictionary<string, string>();
            validMetaData["index"] = indexName;
            validMetaData["source"] = "host";
            validMetaData["sourcetype"] = "log";
            validMetaData["host"] = "customhostname";
            var invalidMetaData = new Dictionary<string, string>();
            invalidMetaData["index"] = "notexistingindex";
            invalidMetaData["source"] = "host";
            invalidMetaData["sourcetype"] = "log";

            var listenerWithWrongToken = new HttpInputTraceListener(
                uri: new Uri("https://127.0.0.1:8089"),
                token: "notexistingtoken",
                metadata: validMetaData);
            var listenerWithWrongUri = new HttpInputTraceListener(
                uri: new Uri("https://127.0.0.1:8087"),
                token: token,
                metadata: validMetaData);
            var listenerWithWrongMetadata = new HttpInputTraceListener(
                uri: new Uri("https://127.0.0.1:8089"),
                token: token,
                metadata: invalidMetaData);


            bool wrongTokenWasRaised = false;
            listenerWithWrongToken.AddLoggingFailureHandler((object sender, HttpInputException e) =>
            {
                wrongTokenWasRaised = true;
            });
            bool wrongUriWasRaised = false;
            listenerWithWrongUri.AddLoggingFailureHandler((object sender, HttpInputException e) =>
            {
                wrongUriWasRaised = true;
            });
            bool wrongMetaDataWasRaised = false;
            listenerWithWrongMetadata.AddLoggingFailureHandler((object sender, HttpInputException e) =>
            {
                wrongMetaDataWasRaised = true;
            });

            trace.Listeners.Add(listenerWithWrongToken);
            trace.Listeners.Add(listenerWithWrongUri);
            trace.Listeners.Add(listenerWithWrongMetadata);
            // Generate data
            int eventCounter = GenerateData(trace);
            Console.WriteLine("{0} events were created, waiting for errors to be raised.", eventCounter);
            Thread.Sleep(30*1000);
            Console.WriteLine(wrongTokenWasRaised ? "Wrong token error was raised." : "Wrong token error was not raised.");
            Console.WriteLine(wrongUriWasRaised ? "Wrong URI error was raised." : "Wrong URI error was not raised.");
            Console.WriteLine(wrongMetaDataWasRaised ? "Wrong metadata error was raised." : "Wrong metadata error was not raised.");
            Console.WriteLine((wrongTokenWasRaised && wrongUriWasRaised && wrongMetaDataWasRaised) ? "Test PASSED." : "Test FAILED.");
            Console.WriteLine();
            return wrongTokenWasRaised && wrongUriWasRaised && wrongMetaDataWasRaised;
        }

        static void Main(string[] args)
        {
            if(args.Length <1)
            {
                Console.WriteLine("Incorrect usage, test name(s) aren't provided.");
                Environment.Exit(3);
            }

            EnableSelfSignedCertificates();
            bool testResults = true;
            foreach (string testName in args)
            {
                switch (testName.ToUpperInvariant())
                {
                    case "SENDEVENTSBATCHEDBYTIME":
                        testResults = SendEventsBatchedByTime() && testResults;
                        break;
                    case "SENDEVENTSBATCHEDBYSIZE":
                        testResults = SendEventsBatchedBySize() && testResults;
                        break;
                    case "SENDEVENTSBATCHEDBYSIZEANDTIME":
                        testResults = SendEventsBatchedBySizeAndTime() && testResults;
                        break;
                    case "SENDEVENTSUNBATCHED":
                        testResults = SendEventsUnBatched() && testResults;
                        break;
                    case "VERIFYERRORSARERAISED":
                        testResults = VerifyErrorsAreRaised() && testResults;
                        break;
                    default:
                        Console.WriteLine("Unknown test '{0}', exiting.", testName);
                        Environment.Exit(3);
                        break;
                }
            }
            Console.WriteLine("All subtests completed, test {0}.", testResults ? "PASSED" : "FAILED");
            Environment.Exit(testResults ? 0 : -1);
        }
    }
}
