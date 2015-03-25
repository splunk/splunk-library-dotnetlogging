using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;

namespace Splunk.Logging
{
    public class TestHttpInput
    {
        private const string HttpInputPath = "/services/receivers/token/";

        // A dummy HTTP input server
        private class HttpServer : HttpInputResendMessageHandler
        {
            public class Response
            {
                public HttpStatusCode Code;
                public string Context;
                public Response(HttpStatusCode code = HttpStatusCode.OK, string context = "{\"text\":\"Success\",\"code\":0}")
                {
                    Code = code;
                    Context = context;
                }
            }
            public Func<string, dynamic, Response> RequestHandler { get; set; }
            public Uri Uri { get { return new Uri("http://localhost:8089"); } }

            public HttpServer(int retriesOnError = 0)
                : base(retriesOnError)
            { }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                HttpResponseMessage responseMessage = new HttpResponseMessage();
                try
                {                    
                    string authorization = request.Headers.Authorization.ToString();
                    string input = await request.Content.ReadAsStringAsync();;
                    dynamic jobj = null;
                    if (input.Contains("}{"))
                    {
                        // batch of events, convert it to a json array
                        input =
                            "[" +
                            input.Replace("}{", "},{") +
                            "]";
                        jobj = JArray.Parse(input);
                    }
                    else
                    {
                        jobj = JObject.Parse(input);
                    }

                    Response response = RequestHandler(authorization, jobj);
                    responseMessage.StatusCode = response.Code;
                    byte[] buf = Encoding.UTF8.GetBytes(response.Context);
                    responseMessage.Content = new StringContent(response.Context);
                }
                catch (Exception e)
                {
                    Assert.True(false, e.ToString());
                }
                if (responseMessage.StatusCode != HttpStatusCode.OK)
                {
                    throw new HttpInputException(
                        code: responseMessage.StatusCode,
                        webException: null,
                        reply: null,
                        response: responseMessage
                    );
                }
                return responseMessage;
            }
        }
 
        [Trait("integration-tests", "Splunk.Logging.HttpInputTraceListener")]
        [Fact]
        public void HttpInputTraceListener()
        {
            HttpServer server = new HttpServer();

            // setup the logger
            var trace = new TraceSource("HttpInputLogger");
            trace.Switch.Level = SourceLevels.All;
            var meta = new Dictionary<string, string>();
            meta["index"] = "main";
            meta["source"] = "localhost";
            meta["sourcetype"] = "log";
            meta["host"] = "demohost";
            trace.Listeners.Add
                (new HttpInputTraceListener(
                    uri: server.Uri, 
                    token: "TOKEN", 
                    metadata: meta, 
                    messageHandler: server));

            // test authentication
            server.RequestHandler = (auth, input) => 
            {
                Assert.True(auth == "Splunk TOKEN", "wrong authentication");
                return new HttpServer.Response(); 
            };
            trace.TraceEvent(TraceEventType.Information, 1, "info");
            Sleep();

            // test metadata
            ulong now =
                (ulong)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            server.RequestHandler = (auth, input) =>
            {
                Assert.True(input.index.Value == "main");
                Assert.True(input.source.Value == "localhost");
                Assert.True(input.sourcetype.Value == "log");
                Assert.True(input.host.Value == "demohost");
                // check that timestamp is correct
                ulong time = ulong.Parse(input.time.Value);
                Assert.True(time - now <= 1);
                return new HttpServer.Response();
            };
            trace.TraceEvent(TraceEventType.Information, 1, "info");
            Sleep();

            // test event info
            server.RequestHandler = (auth, input) =>
            {
                Assert.True(input["event"].id.Value == "123");
                Assert.True(input["event"].severity.Value == "Error");
                Assert.True(input["event"].message.Value == "Test error");
                return new HttpServer.Response();
            };
            trace.TraceEvent(TraceEventType.Error, 123, "Test error");
            Sleep();

            // test trace data
            server.RequestHandler = (auth, input) =>
            {
                Assert.True(input["event"].id.Value == "123");
                Assert.True(input["event"].data[0].Value == "one");
                Assert.True(input["event"].data[1].Value == "two");
                return new HttpServer.Response();
            };
            string[] data = { "one", "two" };
            trace.TraceData(TraceEventType.Error, 123, data);
            Sleep();
        }

        [Trait("integration-tests", "Splunk.Logging.HttpInputTraceListenerBatchingSize")]
        [Fact]
        public void HttpInputTraceListenerBatchingSize()
        {
            HttpServer server = new HttpServer();            

            // setup the logger
            var trace = new TraceSource("HttpInputLogger");
            trace.Switch.Level = SourceLevels.All;
            trace.Listeners.Add(new HttpInputTraceListener(
                uri: server.Uri, token: "TOKEN",
                batchSizeBytes: 200 // an individual event is around 120B, so we expect 2 events per batch
            ));

            // the first event shouldn't be received
            server.RequestHandler = (auth, input) =>
            {
                Assert.True(false); 
                return null;
            };
            trace.TraceEvent(TraceEventType.Information, 1, "first");
            Sleep();

            // now the second event triggers sending 
            server.RequestHandler = (auth, input) =>
            {
                Assert.True(input[0]["event"].message.Value == "first");
                Assert.True(input[1]["event"].message.Value == "second");
                return new HttpServer.Response();
            };
            trace.TraceEvent(TraceEventType.Information, 2, "second");
            Sleep();

        }

        [Trait("integration-tests", "Splunk.Logging.HttpInputTraceListenerBatchingCount")]
        [Fact]
        public void HttpInputTraceListenerBatchingCount()
        {
            HttpServer server = new HttpServer();
 
            // setup the logger
            var trace = new TraceSource("HttpInputLogger");
            trace.Switch.Level = SourceLevels.All;
            trace.Listeners.Add(new HttpInputTraceListener(
                uri: server.Uri, token: "TOKEN", 
                batchSizeCount: 3 
            ));

            // the first event shouldn't be received by the server
            server.RequestHandler = (auth, input) =>
            {
                Assert.True(false);
                return null;
            };
            trace.TraceEvent(TraceEventType.Information, 1, "first");
            Sleep();

            // the second event shouldn't be received as well
            trace.TraceEvent(TraceEventType.Information, 2, "second");
            Sleep();

            // now the third event triggers sending the batch
            server.RequestHandler = (auth, input) =>
            {
                Assert.True(input[0]["event"].message.Value == "first");
                Assert.True(input[1]["event"].message.Value == "second");
                Assert.True(input[2]["event"].message.Value == "third");
                return new HttpServer.Response();
            };
            trace.TraceEvent(TraceEventType.Information, 3, "third");
            Sleep();            
        }

        [Trait("integration-tests", "Splunk.Logging.HttpInputTraceListenerBatchingFlush")]
        [Fact]
        public void HttpInputTraceListenerBatchingFlush()
        {
            HttpServer server = new HttpServer();

            // setup the logger
            var trace = new TraceSource("HttpInputLogger");
            trace.Switch.Level = SourceLevels.All;
            trace.Listeners.Add(new HttpInputTraceListener(
                uri: server.Uri, token: "TOKEN",
                batchSizeCount: int.MaxValue,
                batchSizeBytes: int.MaxValue,
                messageHandler: server));

            // send multiple events, no event should be received immediately 
            server.RequestHandler = (auth, input) =>
            {
                Assert.True(false);
                return null;
            };
            for (int i = 0; i < 1000; i ++)
                trace.TraceEvent(TraceEventType.Information, 1, "hello");            
            Sleep();

            int receivedCount = 0;
            server.RequestHandler = (auth, input) =>
            {
                receivedCount = input.Count;
                return new HttpServer.Response();
            };
            trace.Close(); // flush all events
            Thread.Sleep(500); // wait long enough to receive the events
            Assert.True(receivedCount == 1000);
        }

        [Trait("integration-tests", "Splunk.Logging.HttpInputTraceListenerBatchingTimer")]
        [Fact]
        public void HttpInputTraceListenerBatchingTimer()
        {
            HttpServer server = new HttpServer();

            // setup the logger
            var trace = new TraceSource("HttpInputLogger");
            trace.Switch.Level = SourceLevels.All;
            trace.Listeners.Add(new HttpInputTraceListener(
                uri: server.Uri, token: "TOKEN",
                batchInterval: 1000,
                batchSizeCount: int.MaxValue,
                batchSizeBytes: int.MaxValue,
                messageHandler: server));

            // send multiple events, no event should be received immediately 
            server.RequestHandler = (auth, input) =>
            {
                Assert.True(false);
                return null;
            };
            for (int i = 0; i < 10; i++)
                trace.TraceEvent(TraceEventType.Information, 1, "hello");
            Sleep();

            int receivedCount = 0;
            server.RequestHandler = (auth, input) =>
            {
                receivedCount = input.Count;
                return new HttpServer.Response();
            };
            Thread.Sleep(1500); // wait for more than 1 second, the timer should
            // flush all events            
            Assert.True(receivedCount == 10);
        }

        [Trait("integration-tests", "Splunk.Logging.HttpInputTraceListenerRetry")]
        [Fact]
        public void HttpInputTraceListenerRetry()
        {
            HttpServer server = new HttpServer(1000);

            // setup the logger
            var trace = new TraceSource("HttpInputLogger");
            trace.Switch.Level = SourceLevels.All;
            trace.Listeners.Add(new HttpInputTraceListener(
                uri: server.Uri, token: "TOKEN",
                messageHandler: server));

            server.RequestHandler = (auth, input) =>
            {
                // mimic a server problem that causes resending the data
                return new HttpServer.Response(HttpStatusCode.ServiceUnavailable);
            };
            trace.TraceEvent(TraceEventType.Information, 1, "hello");
            Sleep();
            server.RequestHandler = (auth, input) =>
            {
                Assert.True(input["event"].message.Value == "hello");
                // "fix" the server
                return new HttpServer.Response();   
            };
            Sleep();
        }

        [Trait("integration-tests", "Splunk.Logging.HttpInputTraceListenerErrorHandler")]
        [Fact]
        public void HttpInputTraceListenerErrorHandler()
        {
            HttpServer server = new HttpServer();

            // setup the logger
            var trace = new TraceSource("HttpInputLogger");
            trace.Switch.Level = SourceLevels.All;
            var listener = new HttpInputTraceListener(
                uri: server.Uri, token: "TOKEN",
                messageHandler: server);
            trace.Listeners.Add(listener);

            listener.AddLoggingFailureHandler((object sender, HttpInputException e) =>
            {
                Assert.True(e.StatusCode == HttpStatusCode.ServiceUnavailable);
                Assert.True(e.Events[0].Event.Message == "hello");
            });
            server.RequestHandler = (auth, input) =>
            {
                // mimic a server problem that causes resending the data
                return new HttpServer.Response(code: HttpStatusCode.ServiceUnavailable);
            };
            trace.TraceEvent(TraceEventType.Information, 1, "hello");
            Sleep();
        }

        private void Sleep()
        {
            // logger and server are async thus we need short delays between individual tests
            Thread.Sleep(100); 
        }
    }
}
