using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using Splunk.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Splunk.Logging
{
    class SplunkCliWrapper
    {
        private string splunkCmd = string.Empty;
        private string userName = string.Empty, password = string.Empty;

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
                Console.WriteLine("Failed to create index. {0} {1}", stdOut, stdError);
                Environment.Exit(2);
            }
        }

        public bool SplunkIsRunning()
        {
            string stdOut, stdError;
            ExecuteSplunkCli("status", out stdOut, out stdError);
            return stdOut.Contains("Splunkd: Running");
        }

        public void StartServer()
        {
            if (SplunkIsRunning())
                return;

            string stdOut, stdError;
            Console.WriteLine("Starting Splunk server.");
            Process splunkProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "net.exe",
                    Arguments = "start splunkd",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            if (!ExecuteProcess(splunkProcess, out stdOut, out stdError))
            {
                Console.WriteLine("Failed to start Splunk. {0} {1}", stdOut, stdError);
                Environment.Exit(2);
            }
            Console.WriteLine("Splunk started.");
        }

        public void StopServer()
        {
            if (!SplunkIsRunning())
                return;

            string stdOut, stdError;
            Console.WriteLine("Stopping Splunk server.");
            if (!ExecuteSplunkCli("stop", out stdOut, out stdError))
            {
                Console.WriteLine("Failed to stop Splunk. {0} {1}", stdOut, stdError);
                Environment.Exit(2);
            }
            Console.WriteLine("Splunk stopped.");
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

        public void WaitForIndexingToComplete(string indexName, double startTime = 0, int stabilityPeriod = 10)
        {
            string query = string.Format(CultureInfo.InvariantCulture, "index={0} earliest={1:F2}", indexName, startTime);
            int eventCount = GetSearchCount(query);
            for (int i = 0; i < stabilityPeriod; i++) // Exit only if there were no indexing activities for %stabilityPeriod% straight seconds
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

        public static bool ExecuteProcess(Process proc, out string stdOut, out string stdError)
        {
            proc.Start();
            stdOut = proc.StandardOutput.ReadToEnd();
            stdError = proc.StandardError.ReadToEnd();
            if (proc.ExitCode != 0)
            {
                return false;
            }
            return true;
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
            return ExecuteProcess(splunkProcess, out stdOut, out stdError);
        }

        private bool ExecuteSplunkCliHttpEventCollector(string command, out string stdOut, out string stdError)
        {
            return ExecuteSplunkCli(string.Format(CultureInfo.InvariantCulture, "http-event-collector {0} -uri https://127.0.0.1:8089", command), out stdOut, out stdError);
        }

        public void EnableHttp()
        {
            string stdOut, stdError;
            if (!ExecuteSplunkCliHttpEventCollector("enable", out stdOut, out stdError))
            {
                Console.WriteLine("Failed to execute 'enable' command. {0} {1}", stdOut, stdError);
                Environment.Exit(2);
            }
        }

        public void DeleteToken(string tokenName)
        {
            string stdOut, stdError;
            ExecuteSplunkCliHttpEventCollector(string.Format(CultureInfo.InvariantCulture, "delete -name {0}", tokenName), out stdOut, out stdError);
        }

        public string CreateToken(string tokenName, string indexes = null, string index = null)
        {
            string stdOut, stdError;
            string createCmd = string.Format(CultureInfo.InvariantCulture, "create -name {0}", tokenName);
            if (!string.IsNullOrEmpty(indexes))
                createCmd += string.Format(CultureInfo.InvariantCulture, " -indexes {0}", indexes);
            if (!string.IsNullOrEmpty(index))
                createCmd += string.Format(CultureInfo.InvariantCulture, " -index {0}", index);
            if (!ExecuteSplunkCliHttpEventCollector(createCmd, out stdOut, out stdError))
            {
                Console.WriteLine("Failed to create token. {0} {1}", stdOut, stdError);
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
            idx1 = output.IndexOf(":", idx1 + 1, StringComparison.Ordinal) + 1;
            while (output[idx1] == ' ')
                idx1++;
            int idx2 = output.IndexOf("service", idx1, StringComparison.Ordinal) - 1;
            while (output[idx2] == ' ')
                idx2--;
            this.splunkCmd = output.Substring(idx1, idx2 - idx1 + 1).Replace("splunkd.exe", "splunk.exe");
            this.StartServer();
            EnableSelfSignedCertificates();
        }
    }

    public class TestWithSplunk
    {
        #region Methods used by tests
        private static void GenerateDataWaitForIndexingCompletion(SplunkCliWrapper splunk, string indexName, double testStartTime, TraceSource trace)
        {
            // Generate data
            int eventCounter = GenerateData(trace);
            string searchQuery = "index=" + indexName;
            Console.WriteLine("{0} events were created, waiting for indexing to complete.", eventCounter);
            splunk.WaitForIndexingToComplete(indexName);
            int eventsFound = splunk.GetSearchCount(searchQuery);
            Console.WriteLine("Indexing completed, {0} events were found. Elapsed time {1:F2} seconds", eventsFound, SplunkCliWrapper.GetEpochTime() - testStartTime);
            // Verify all events were indexed correctly
            Assert.Equal(eventCounter, eventsFound);
            List<string> searchResults = splunk.GetSearchResults(searchQuery);
            Assert.Equal(searchResults.Count, eventsFound);
            for (int eventId = 0; eventId < eventCounter; eventId++)
            {
                string expected = string.Format("This is event {0}", eventId);
                Assert.NotNull(searchResults.FirstOrDefault(s => s.Contains(expected)));
            }
            // Verify metadata
            List<string> metaData = splunk.GetMetadataResults(searchQuery);
            Assert.Equal(metaData[0], "customhostname");
            Assert.Equal(metaData[1], "log");
            Assert.Equal(metaData[2], "host");
        }
        private static int GenerateData(TraceSource trace, int eventsPerLoop = 50)
        {
            int eventCounter = 0, id = 0;
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
            Console.WriteLine("Enabled HTTP event collector.");
            splunk.DeleteToken(tokenName);
            string token = splunk.CreateToken(tokenName, indexes: indexName, index: indexName);
            Console.WriteLine("Created token {0}.", tokenName);
            splunk.DeleteIndex(indexName);
            splunk.CreateIndex(indexName);
            Console.WriteLine("Created index {0}.", indexName);
            return token;
        }
        #endregion
        /*
        #region Tests implementation
        [Trait("functional-tests", "StallWrite")]
        [Fact]
        public static void StallWrite()
        {
            string tokenName = "stallwritetoken";
            string indexName = "stallwriteindex";
            SplunkCliWrapper splunk = new SplunkCliWrapper();
            double testStartTime = SplunkCliWrapper.GetEpochTime();
            string token = CreateIndexAndToken(splunk, tokenName, indexName);

            const string baseUrl = "https://127.0.0.1:8088";
            string postData = "{ \"event\": { \"data\": \"test event\" } }";

            List<HttpWebRequest> requests = new List<HttpWebRequest>();
            List<Stream> streams = new List<Stream>();
            byte[] byteArray = Encoding.UTF8.GetBytes(postData);
            for (int i = 0; i < 10000; i++)
            {
                var request = WebRequest.Create(baseUrl + "/services/collector") as HttpWebRequest;
                requests.Add(request);
                request.Timeout = 60 * 1000;
                request.KeepAlive = true;
                request.Method = "POST";
                request.Headers.Add("Authorization", "Splunk " + token);
                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = byteArray.Length * 2;
                Stream dataStream = request.GetRequestStream();
                streams.Add(dataStream);
                dataStream.Write(byteArray, 0, byteArray.Length);
            }
        } 
        [Trait("functional-tests", "SendEventsBatchedByTime")]
        [Fact]
        public static void SendEventsBatchedByTime()
        {
            string tokenName = "batchedbytimetoken";
            string indexName = "batchedbytimeindex";
            SplunkCliWrapper splunk = new SplunkCliWrapper();
            double testStartTime = SplunkCliWrapper.GetEpochTime();
            string token = CreateIndexAndToken(splunk, tokenName, indexName);

            var trace = new TraceSource("HttpEventCollectorLogger");
            trace.Switch.Level = SourceLevels.All;
            var meta = new HttpEventCollectorEventInfo.Metadata(index: indexName, source: "host", sourceType: "log", host: "customhostname");
            var listener = new HttpEventCollectorTraceListener(
                uri: new Uri("https://127.0.0.1:8088"),
                token: token,
                metadata: meta,
                batchInterval: 1000);
            trace.Listeners.Add(listener);

            GenerateDataWaitForIndexingCompletion(splunk, indexName, testStartTime, trace);
            trace.Close();
        }

        [Trait("functional-tests", "SendEventsBatchedBySize")]
        [Fact]
        static void SendEventsBatchedBySize()
        {
            string tokenName = "batchedbysizetoken";
            string indexName = "batchedbysizeindex";
            SplunkCliWrapper splunk = new SplunkCliWrapper();
            double testStartTime = SplunkCliWrapper.GetEpochTime();
            string token = CreateIndexAndToken(splunk, tokenName, indexName);

            var trace = new TraceSource("HttpEventCollectorLogger");
            trace.Switch.Level = SourceLevels.All;
            var meta = new HttpEventCollectorEventInfo.Metadata(index: indexName, source: "host", sourceType: "log", host: "customhostname");
            var listener = new HttpEventCollectorTraceListener(
                uri: new Uri("https://127.0.0.1:8088"),
                token: token,
                metadata: meta,
                batchSizeCount: 50);
            trace.Listeners.Add(listener);

            GenerateDataWaitForIndexingCompletion(splunk, indexName, testStartTime, trace);
            trace.Close();
        }

        [Trait("functional-tests", "SendEventsBatchedBySizeAndTime")]
        [Fact]
        static void SendEventsBatchedBySizeAndTime()
        {
            string tokenName = "batchedbysizeandtimetoken";
            string indexName = "batchedbysizeandtimeindex";
            SplunkCliWrapper splunk = new SplunkCliWrapper();
            double testStartTime = SplunkCliWrapper.GetEpochTime();
            string token = CreateIndexAndToken(splunk, tokenName, indexName);

            var trace = new TraceSource("HttpEventCollectorLogger");
            trace.Switch.Level = SourceLevels.All;
            var meta = new HttpEventCollectorEventInfo.Metadata(index: indexName, source: "host", sourceType: "log", host: "customhostname");
            var listener = new HttpEventCollectorTraceListener(
                uri: new Uri("https://127.0.0.1:8088"),
                token: token,
                metadata: meta,
                batchSizeCount: 60, batchInterval: 2000);
            trace.Listeners.Add(listener);

            GenerateDataWaitForIndexingCompletion(splunk, indexName, testStartTime, trace);
            trace.Close();
        }

        [Trait("functional-tests", "SendEventsUnBatched")]
        [Fact]
        static void SendEventsUnBatched()
        {
            string tokenName = "unbatchedtoken";
            string indexName = "unbatchedindex";
            SplunkCliWrapper splunk = new SplunkCliWrapper();
            double testStartTime = SplunkCliWrapper.GetEpochTime();
            string token = CreateIndexAndToken(splunk, tokenName, indexName);

            var trace = new TraceSource("HttpEventCollectorLogger");
            trace.Switch.Level = SourceLevels.All;
            var meta = new HttpEventCollectorEventInfo.Metadata(index: indexName, source: "host", sourceType: "log", host: "customhostname");
            var listener = new HttpEventCollectorTraceListener(
                uri: new Uri("https://127.0.0.1:8088"),
                token: token,
                metadata: meta);
            trace.Listeners.Add(listener);

            GenerateDataWaitForIndexingCompletion(splunk, indexName, testStartTime, trace);
            trace.Close();
        }

        [Trait("functional-tests", "VerifyErrorsAreRaised")]
        [Fact]
        static void VerifyErrorsAreRaised()
        {
            string indexName = "errorindex";
            string tokenName = "errortoken";
            double testStartTime = SplunkCliWrapper.GetEpochTime();
            SplunkCliWrapper splunk = new SplunkCliWrapper();
            string token = CreateIndexAndToken(splunk, tokenName, indexName);

            var trace = new TraceSource("HttpEventCollectorLogger");
            trace.Switch.Level = SourceLevels.All;

            var validMetaData = new HttpEventCollectorEventInfo.Metadata(index: indexName, source: "host", sourceType: "log", host: "customhostname");
            var invalidMetaData = new HttpEventCollectorEventInfo.Metadata(index: "notexistingindex", source: "host", sourceType: "log", host: "customhostname");

            var listenerWithWrongToken = new HttpEventCollectorTraceListener(
                uri: new Uri("https://127.0.0.1:8088"),
                token: "notexistingtoken",
                metadata: validMetaData);
            var listenerWithWrongUri = new HttpEventCollectorTraceListener(
                uri: new Uri("https://127.0.0.1:8087"),
                token: token,
                metadata: validMetaData);
            var listenerWithWrongMetadata = new HttpEventCollectorTraceListener(
                uri: new Uri("https://127.0.0.1:8088"),
                token: token,
                metadata: invalidMetaData);

            bool wrongTokenWasRaised = false;
            listenerWithWrongToken.AddLoggingFailureHandler((HttpEventCollectorException e) =>
            {
                wrongTokenWasRaised = true;
            });
            bool wrongUriWasRaised = false;
            listenerWithWrongUri.AddLoggingFailureHandler((HttpEventCollectorException e) =>
            {
                wrongUriWasRaised = true;
            });
            bool wrongMetaDataWasRaised = false;
            listenerWithWrongMetadata.AddLoggingFailureHandler((HttpEventCollectorException e) =>
            {
                wrongMetaDataWasRaised = true;
            });

            trace.Listeners.Add(listenerWithWrongToken);
            trace.Listeners.Add(listenerWithWrongUri);
            trace.Listeners.Add(listenerWithWrongMetadata);
            // Generate data
            int eventCounter = GenerateData(trace);
            Console.WriteLine("{0} events were created, waiting for errors to be raised.", eventCounter);
            Thread.Sleep(30 * 1000);
            trace.Close();
            Assert.True(wrongTokenWasRaised);
            Assert.True(wrongUriWasRaised);
            Assert.True(wrongMetaDataWasRaised);
        }
        
        [Trait("functional-tests", "VerifyFlushEvents")]
        [Fact]
        public  void VerifyFlushEvents()
        {
            string tokenName = "flusheventtoken";
            string indexName = "flusheventdindex";
            SplunkCliWrapper splunk = new SplunkCliWrapper();
            double testStartTime = SplunkCliWrapper.GetEpochTime();

            string token = CreateIndexAndToken(splunk, tokenName, indexName);
            splunk.StopServer();
            Thread.Sleep(5 * 1000);

            var trace = new TraceSource("HttpEventCollectorLogger");
            trace.Switch.Level = SourceLevels.All;
            var meta = new HttpEventCollectorEventInfo.Metadata(index: indexName, source: "host", sourceType: "log", host: "customhostname");
            var listener = new HttpEventCollectorTraceListener(
                uri: new Uri("https://127.0.0.1:8088"),
                token: token,
                retriesOnError: int.MaxValue,
                metadata: meta);
            trace.Listeners.Add(listener);

            // Generate data, wait a little bit so retries are happenning and start Splunk. Expecting to see all data make it
            const int eventsToGeneratePerLoop = 250;
            const int expectedCount = eventsToGeneratePerLoop * 7;
            int eventCounter = GenerateData(trace, eventsPerLoop: eventsToGeneratePerLoop);
            splunk.StartServer();
            trace.Close();

            // Verify every event made to Splunk
            splunk.WaitForIndexingToComplete(indexName, stabilityPeriod: 30);
            int eventsFound = splunk.GetSearchCount("index=" + indexName);
            Assert.Equal(expectedCount, eventsFound);
        }

        [Trait("functional-tests", "VerifyEventsAreInOrder")]
        [Fact]
        static void VerifyEventsAreInOrder()
        {
            string tokenName = "verifyeventsareinordertoken";
            string indexName = "verifyeventsareinorderindex";
            SplunkCliWrapper splunk = new SplunkCliWrapper();
            double testStartTime = SplunkCliWrapper.GetEpochTime();
            string token = CreateIndexAndToken(splunk, tokenName, indexName);

            var trace = new TraceSource("HttpEventCollectorLogger");
            trace.Switch.Level = SourceLevels.All;
            var meta = new HttpEventCollectorEventInfo.Metadata(index: indexName, source: "host", sourceType: "log", host: "customhostname");
            var listener = new HttpEventCollectorTraceListener(
                uri: new Uri("https://127.0.0.1:8088"),
                token: token,
                metadata: meta);
            trace.Listeners.Add(listener);

            // Generate data
            int totalEvents = 1000;
            string[] filer = new string[2];
            filer[0] = new string('s', 1);
            filer[1] = new string('l', 100000);
            for (int i = 0; i < totalEvents; i++)
            {
                trace.TraceInformation(string.Format("TraceInformation. This is event {0}. {1}", i, filer[i%2]));
            }

            string searchQuery = "index=" + indexName;
            Console.WriteLine("{0} events were created, waiting for indexing to complete.", totalEvents);
            splunk.WaitForIndexingToComplete(indexName);
            int eventsFound = splunk.GetSearchCount(searchQuery);
            Console.WriteLine("Indexing completed, {0} events were found. Elapsed time {1:F2} seconds", eventsFound, SplunkCliWrapper.GetEpochTime() - testStartTime);
            // Verify all events were indexed correctly and in order
            Assert.Equal(totalEvents, eventsFound);
            List<string> searchResults = splunk.GetSearchResults(searchQuery);
            Assert.Equal(searchResults.Count, eventsFound);
            for (int i = 0; i< totalEvents; i++)
            {
                int id = totalEvents - i - 1;
                string expected = string.Format("TraceInformation. This is event {0}. {1}", id, filer[id % 2]);
                Assert.True(searchResults[i].Contains(expected));
            }
            trace.Close();
        }

        #endregion*/
    }
}
