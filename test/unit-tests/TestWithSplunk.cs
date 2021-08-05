using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using Xunit;

namespace Splunk.Logging
{
    public class TestWithSplunk
    {
        #region Methods used by tests
        private static void GenerateDataWaitForIndexingCompletion(SplunkTestWrapper splunk, string indexName, double testStartTime, TraceSource trace)
        {
            // Generate data
            int eventCounter = GenerateData(trace);
            string searchQuery = "index=" + indexName;
            Console.WriteLine("{0} events were created, waiting for indexing to complete.", eventCounter);
            splunk.WaitForIndexingToComplete(indexName);
            int eventsFound = splunk.GetSearchCount(searchQuery);
            Console.WriteLine("Indexing completed, {0} events were found. Elapsed time {1:F2} seconds", eventsFound, SplunkTestWrapper.GetEpochTime() - testStartTime);
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

        private static string CreateIndexAndToken(SplunkTestWrapper splunk, string tokenName, string indexName)
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

        #region Tests implementation
        [Trait("functional-tests", "SendEventsBatchedByTime")]
        [Fact]
        public static void SendEventsBatchedByTime()
        {
            string tokenName = "batchedbytimetoken";
            string indexName = "batchedbytimeindex";
            SplunkTestWrapper splunk = new SplunkTestWrapper();
            double testStartTime = SplunkTestWrapper.GetEpochTime();
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
            SplunkTestWrapper splunk = new SplunkTestWrapper();
            double testStartTime = SplunkTestWrapper.GetEpochTime();
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
            SplunkTestWrapper splunk = new SplunkTestWrapper();
            double testStartTime = SplunkTestWrapper.GetEpochTime();
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
            SplunkTestWrapper splunk = new SplunkTestWrapper();
            double testStartTime = SplunkTestWrapper.GetEpochTime();
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
            double testStartTime = SplunkTestWrapper.GetEpochTime();
            SplunkTestWrapper splunk = new SplunkTestWrapper();
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

        // Stop and Start Server API is not exist so commented 'VerifyFlushEvents' test case
        //[Trait("functional-tests", "VerifyFlushEvents")]
        //[Fact]
        //public void VerifyFlushEvents()
        //{
        //    string tokenName = "flusheventtoken";
        //    string indexName = "flusheventdindex";
        //    SplunkTestWrapper splunk = new SplunkTestWrapper();
        //    double testStartTime = SplunkTestWrapper.GetEpochTime();

        //    string token = CreateIndexAndToken(splunk, tokenName, indexName);
        //    splunk.StopServer();
        //    Thread.Sleep(5 * 1000);

        //    var trace = new TraceSource("HttpEventCollectorLogger");
        //    trace.Switch.Level = SourceLevels.All;
        //    var meta = new HttpEventCollectorEventInfo.Metadata(index: indexName, source: "host", sourceType: "log", host: "customhostname");
        //    var listener = new HttpEventCollectorTraceListener(
        //        uri: new Uri("https://127.0.0.1:8088"),
        //        token: token,
        //        retriesOnError: int.MaxValue,
        //        metadata: meta);
        //    trace.Listeners.Add(listener);

        //    // Generate data, wait a little bit so retries are happenning and start Splunk. Expecting to see all data make it
        //    const int eventsToGeneratePerLoop = 250;
        //    const int expectedCount = eventsToGeneratePerLoop * 7;
        //    int eventCounter = GenerateData(trace, eventsPerLoop: eventsToGeneratePerLoop);
        //    splunk.StartServer();
        //    trace.Close();

        //    // Verify every event made to Splunk
        //    splunk.WaitForIndexingToComplete(indexName, stabilityPeriod: 30);
        //    int eventsFound = splunk.GetSearchCount("index=" + indexName);
        //    Assert.Equal(expectedCount, eventsFound);
        //}

        [Trait("functional-tests", "VerifyEventsAreInOrder")]
        [Fact]
        static void VerifyEventsAreInOrder()
        {
            string tokenName = "verifyeventsareinordertoken";
            string indexName = "verifyeventsareinorderindex";
            SplunkTestWrapper splunk = new SplunkTestWrapper();
            double testStartTime = SplunkTestWrapper.GetEpochTime();
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
                trace.TraceInformation(string.Format("TraceInformation. This is event {0}. {1}", i, filer[i % 2]));
            }

            string searchQuery = "index=" + indexName;
            Console.WriteLine("{0} events were created, waiting for indexing to complete.", totalEvents);
            splunk.WaitForIndexingToComplete(indexName);
            int eventsFound = splunk.GetSearchCount(searchQuery);
            Console.WriteLine("Indexing completed, {0} events were found. Elapsed time {1:F2} seconds", eventsFound, SplunkTestWrapper.GetEpochTime() - testStartTime);
            // Verify all events were indexed correctly and in order
            Assert.Equal(totalEvents, eventsFound);
            List<string> searchResults = splunk.GetSearchResults(searchQuery);
            Assert.Equal(searchResults.Count, eventsFound);
            for (int i = 0; i < totalEvents; i++)
            {
                int id = totalEvents - i - 1;
                string expected = string.Format("TraceInformation. This is event {0}", id);
                Assert.True(searchResults[i].Contains(expected));
            }
            trace.Close();
        }
        #endregion
    }
}
