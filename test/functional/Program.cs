using Splunk.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace functional
{
    class Splunk
    {
        private string splunkCmd = string.Empty;
        private string userName = string.Empty, password = string.Empty;

        public void DeleteIndex(string indexName)
        {
            string stdOut, stdError;
            ExecuteSplunkCli(string.Format("remove index {0}", indexName), out stdOut, out stdError);
        }

        public void CreateIndex(string indexName)
        {
            string stdOut, stdError;
            if (!ExecuteSplunkCli(string.Format("add index {0}", indexName), out stdOut, out stdError))
            {
                Console.WriteLine("Failed to create index. {1} {2}", stdOut, stdError);
                Environment.Exit(2);
            }
        }

        public int GetSearchCount(string searchQuery)
        {
            string stdOut, stdError;
            if (!ExecuteSplunkCli(string.Format("search \"{0} | stats count\" -preview false", searchQuery), out stdOut, out stdError))
            {
                Console.WriteLine("Failed to run search query '{0}'. {1} {2}", searchQuery, stdOut, stdError);
                Environment.Exit(2);
            }
            return Convert.ToInt32(stdOut.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[2]);
        }

        public double GetEpochTime()
        {
            TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            return t.TotalSeconds;
        }

        public void WaitForIndexingToComplete(string indexName, double startTime)
        {
            string query = string.Format("index={0} earliest={1:F2}", indexName, startTime);
            int eventCount = GetSearchCount(query);
            for (int i = 0; i < 4; i++)
            {
                do
                {
                    Thread.Sleep(5000);
                    int updatedEventCount = GetSearchCount(query);
                    if (updatedEventCount == eventCount)
                        break;
                    eventCount = updatedEventCount;
                } while (true);
            }
        }

        private bool ExecuteSplunkCli(string command, out string stdOut, out string stdError)
        {
            Process splunkProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = splunkCmd,
                    Arguments = string.Format("{0} -auth {1}:{2}", command, userName, password),
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
            return ExecuteSplunkCli(string.Format("http-input {0} -uri https://127.0.0.1:8089", command), out stdOut, out stdError);
        }

        public void EnableHttp()
        {
            string stdOut, stdError;
            if (!ExecuteSplunkCliHttpInput("enable", out stdOut, out stdError))
            {
                Console.WriteLine("Failed to execute 'enable' command. {1} {2}", stdOut, stdError);
                Environment.Exit(2);
            }
        }

        public void DeleteToken(string tokenName)
        {
            string stdOut, stdError;
            ExecuteSplunkCliHttpInput(string.Format("delete -name {0}", tokenName), out stdOut, out stdError);
        }

        public string CreateToken(string tokenName, string indexes = null, string index = null)
        {
            string stdOut, stdError;
            string createCmd = string.Format("create -name {0}", tokenName);
            if (!string.IsNullOrEmpty(indexes))
                createCmd += string.Format(" -indexes {0}", indexes);
            if (!string.IsNullOrEmpty(index))
                createCmd += string.Format(" -index {0}", index);
            if (!ExecuteSplunkCliHttpInput(createCmd, out stdOut, out stdError))
            {
                Console.WriteLine("Failed to create token. {1} {2}", stdOut, stdError);
                Environment.Exit(2);
            }
            int idx1 = stdOut.IndexOf("token=") + 6, idx2 = idx1;
            while (stdOut[idx2] != '\r')
                idx2++;
            string result = stdOut.Substring(idx1, idx2 - idx1);
            return result;
        }

        public Splunk(string userName = "admin", string password = "changeme")
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
            int idx1 = output.IndexOf("BINARY_PATH_NAME");
            idx1 = output.IndexOf(":", idx1 + 1) + 1;
            while (output[idx1] == ' ')
                idx1++;
            int idx2 = output.IndexOf("service", idx1) - 1;
            while (output[idx2] == ' ')
                idx2--;
            splunkCmd = output.Substring(idx1, idx2 - idx1 + 1).Replace("splunkd.exe", "splunk.exe");
        }
    }

    class Program
    {
        private static bool GenerateDataWaitForIndexingCompletion(Splunk splunk, string indexName, double testStartTime, TraceSource trace)
        {
            // Generate data
            int eventCounter = GenerateData(trace);
            Console.WriteLine("{0} events were created, waiting for indexing to complete.", eventCounter);
            splunk.WaitForIndexingToComplete(indexName, testStartTime);
            int eventsFound = splunk.GetSearchCount("index=" + indexName);
            Console.WriteLine("Indexing completed, {0} events were found. Elapsed time {1:F2} seconds", eventsFound, splunk.GetEpochTime() - testStartTime);
            Console.WriteLine(eventCounter == eventsFound ? "Test PASSED." : "Test FAILED.");
            Console.WriteLine();
            return eventCounter == eventsFound;
        }

        private static int GenerateData(TraceSource trace)
        {
            int eventCounter = 0, id = 0;
            const int eventsPerLoop = 50;
            foreach (TraceEventType eventType in new TraceEventType[] { TraceEventType.Error, TraceEventType.Information, TraceEventType.Warning })
            {
                for (int i = 0; i < eventsPerLoop; i++)
                {
                    trace.TraceData(eventType, id, new string[] { "TraceData", eventType.ToString() });
                    id++;
                    eventCounter++;
                }
            }
            foreach (TraceEventType eventType in new TraceEventType[] { TraceEventType.Error, TraceEventType.Information, TraceEventType.Warning })
            {
                for (int i = 0; i < eventsPerLoop; i++)
                {
                    trace.TraceEvent(eventType, id, "TraceEvent " + eventType.ToString());
                    id++;
                    eventCounter++;
                }
            }
            for (int i = 0; i < eventsPerLoop; i++, id++, eventCounter++)
            {
                trace.TraceInformation("TraceInformation");
            }
            return eventCounter;
        }

        private static string CreateIndexAndToken(Splunk splunk, string tokenName, string indexName)
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
            Splunk splunk = new Splunk();
            double testStartTime = splunk.GetEpochTime();
            string token = CreateIndexAndToken(splunk, tokenName, indexName);
            Thread.Sleep(1000);
            Console.WriteLine("Test SendEventsBatchedByTime started.");

            var trace = new TraceSource("HttpInputLogger");
            trace.Switch.Level = SourceLevels.All;
            var meta = new Dictionary<string, string>();
            meta["index"] = indexName;
            meta["source"] = "host";
            meta["sourcetype"] = "log";
            var listener = new HttpInputTraceListener(
                uri: new Uri("https://127.0.0.1:8089"),
                token: token,
                metadata: meta,
                batchInterval: 1000);
            trace.Listeners.Add(listener);

            return GenerateDataWaitForIndexingCompletion(splunk, indexName, testStartTime, trace);
        }

        static bool SendEventsBatchedBySize()
        {
            string tokenName = "batchedbysizetoken";
            string indexName = "batchedbysizeindex";
            Splunk splunk = new Splunk();
            double testStartTime = splunk.GetEpochTime();
            string token = CreateIndexAndToken(splunk, tokenName, indexName);
            Thread.Sleep(1000);
            Console.WriteLine("Test SendEventsBatchedBySize started.");

            var trace = new TraceSource("HttpInputLogger");
            trace.Switch.Level = SourceLevels.All;
            var meta = new Dictionary<string, string>();
            meta["index"] = indexName;
            meta["source"] = "host";
            meta["sourcetype"] = "log";
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
            Splunk splunk = new Splunk();
            double testStartTime = splunk.GetEpochTime();
            string token = CreateIndexAndToken(splunk, tokenName, indexName);
            Thread.Sleep(1000);
            Console.WriteLine("Test SendEventsBatchedBySizeAndTime started.");

            var trace = new TraceSource("HttpInputLogger");
            trace.Switch.Level = SourceLevels.All;
            var meta = new Dictionary<string, string>();
            meta["index"] = indexName;
            meta["source"] = "host";
            meta["sourcetype"] = "log";
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
            Splunk splunk = new Splunk();
            double testStartTime = splunk.GetEpochTime();
            string token = CreateIndexAndToken(splunk, tokenName, indexName);
            Thread.Sleep(1000);
            Console.WriteLine("Test SendEventsUnBatched started.");

            var trace = new TraceSource("HttpInputLogger");
            trace.Switch.Level = SourceLevels.All;
            var meta = new Dictionary<string, string>();
            meta["index"] = indexName;
            meta["source"] = "host";
            meta["sourcetype"] = "log";
            var listener = new HttpInputTraceListener(
                uri: new Uri("https://127.0.0.1:8089"),
                token: token,
                metadata: meta);
            trace.Listeners.Add(listener);

            return GenerateDataWaitForIndexingCompletion(splunk, indexName, testStartTime, trace);
        }

        static bool VerifyErrorsAreRaised()
        {
            string tokenName = "errortoken";
            string indexName = "errorindex";
            Splunk splunk = new Splunk();
            double testStartTime = splunk.GetEpochTime();
            string token = CreateIndexAndToken(splunk, tokenName, indexName);
            Thread.Sleep(1000);
            Console.WriteLine("Test VerifyErrorsAreRaised started.");

            var trace = new TraceSource("HttpInputLogger");
            trace.Switch.Level = SourceLevels.All;
            var meta = new Dictionary<string, string>();
            meta["index"] = indexName;
            meta["source"] = "host";
            meta["sourcetype"] = "log";
            var listener = new HttpInputTraceListener(
                uri: new Uri("https://127.0.0.1:8089"),
                token: "notexistingtoken",
                metadata: meta);
            bool errorWasRaised = false;
            listener.AddLoggingFailureHandler((object sender, HttpInputException e) =>
            {
                errorWasRaised = true;
            });

            trace.Listeners.Add(listener);
            // Generate data
            int eventCounter = GenerateData(trace);
            Console.WriteLine("{0} events were created, waiting for error to be raised.", eventCounter);
            Thread.Sleep(15*1000);
            Console.WriteLine("Wait completed, error was {0}. Elapsed time {1:F2} seconds", errorWasRaised ? "detected" : "not detected", splunk.GetEpochTime() - testStartTime);
            Console.WriteLine(errorWasRaised ? "Test PASSED." : "Test FAILED.");
            Console.WriteLine();
            return errorWasRaised;
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
                switch (testName.ToLower())
                {
                    case "sendeventsbatchedbytime":
                        testResults = SendEventsBatchedByTime() && testResults;
                        break;
                    case "sendeventsbatchedbysize":
                        testResults = SendEventsBatchedBySize() && testResults;
                        break;
                    case "sendeventsbatchedbysizeandtime":
                        testResults = SendEventsBatchedBySizeAndTime() && testResults;
                        break;
                    case "sendeventsunbatched":
                        testResults = SendEventsUnBatched() && testResults;
                        break;
                    case "verifyerrorsareraised":
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
