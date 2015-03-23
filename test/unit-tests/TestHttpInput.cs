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

namespace Splunk.Logging
{
    public class TestHttpInput
    {
        private const string HttpInputPath = "/services/receivers/token/";

        // A dummy http input server
        private class HttpServer
        {
            private static int port = 5000;
            private string uri;
            private readonly HttpListener listener = new HttpListener();

            public class Response
            {
                public int Code = 200;
                public string Context = "{\"text\":\"Success\",\"code\":0}";
            }
            public Func<string, dynamic, Response> Method { get; set; }

            public HttpServer()
            {
                // the tests are running simultaneously thus we start a new multiple 
                // http servers with different ports 
                port++;
                uri = "http://localhost:" + port;
                string url = uri + HttpInputPath;
                listener.Prefixes.Add(url);
                listener.Start();
                Run();
            }

            public string Uri { get { return uri; } }
            public void Run()
            {
                ThreadPool.QueueUserWorkItem((wi) =>
                {
                    try
                    {
                        while (listener.IsListening)
                        {
                            ThreadPool.QueueUserWorkItem((obj) =>
                            {
                                var context = obj as HttpListenerContext;
                                try
                                {
                                    string authorization = context.Request.Headers.Get("Authorization");                                    
                                    string input = new StreamReader(context.Request.InputStream).ReadToEnd();
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
                                    
                                    Response response = Method(authorization, jobj);
                                    context.Response.StatusCode = response.Code;
                                    byte[] buf = Encoding.UTF8.GetBytes(response.Context);
                                    context.Response.ContentLength64 = buf.Length;
                                    context.Response.OutputStream.Write(buf, 0, buf.Length);                                    
                                }
                                catch (Exception e) 
                                {
                                    Assert.True(false, e.ToString());   
                                } 
                                finally
                                {
                                    // always close the stream
                                    context.Response.OutputStream.Close();
                                }
                            }, listener.GetContext());
                        }
                    }
                    catch { } // suppress any exceptions
                });
            }

            public void Stop()
            {
                listener.Stop();
                listener.Close();
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
            trace.Listeners.Add(new HttpInputTraceListener(uri: server.Uri, token: "TOKEN", metadata: meta));

            // test authentication
            server.Method = (auth, input) => 
            {
                Assert.True(auth == "Splunk TOKEN", "wrong authentication");
                return new HttpServer.Response(); 
            };
            trace.TraceEvent(TraceEventType.Information, 1, "info");
            Sleep();

            // test metadata
            ulong now =
                (ulong)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            server.Method = (auth, input) =>
            {
                Assert.True(input.index.Value == "main");
                Assert.True(input.source.Value == "localhost");
                Assert.True(input.sourcetype.Value == "log");
                // check that timestamp is correct
                ulong time = ulong.Parse(input.time.Value);
                Assert.True(time - now <= 1);
                return new HttpServer.Response();
            };
            trace.TraceEvent(TraceEventType.Information, 1, "info");
            Sleep();

            // test event info
            server.Method = (auth, input) =>
            {
                Assert.True(input["event"].id.Value == "123");
                Assert.True(input["event"].severity.Value == "Error");
                Assert.True(input["event"].message.Value == "Test error");
                return new HttpServer.Response();
            };
            trace.TraceEvent(TraceEventType.Error, 123, "Test error");
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
            server.Method = (auth, input) =>
            {
                Assert.True(false); 
                return null;
            };
            trace.TraceEvent(TraceEventType.Information, 1, "first");
            Sleep();

            // now the second event triggers sending 
            server.Method = (auth, input) =>
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
            server.Method = (auth, input) =>
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
            server.Method = (auth, input) =>
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
                batchSizeCount: uint.MaxValue,
                batchSizeBytes: uint.MaxValue));

            // send multiple events, no event should be received immediately 
            server.Method = (auth, input) =>
            {
                Assert.True(false);
                return null;
            };
            for (int i = 0; i < 1000; i ++)
                trace.TraceEvent(TraceEventType.Information, 1, "hello");            
            Sleep();

            int receivedCount = 0;
            server.Method = (auth, input) =>
            {
                receivedCount = input.Count;
                return new HttpServer.Response();
            };
            trace.Close(); // flush all events
            Sleep();
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
                batchSizeCount: uint.MaxValue,
                batchSizeBytes: uint.MaxValue));

            // send multiple events, no event should be received immediately 
            server.Method = (auth, input) =>
            {
                Assert.True(false);
                return null;
            };
            for (int i = 0; i < 10; i++)
                trace.TraceEvent(TraceEventType.Information, 1, "hello");
            Sleep();

            int receivedCount = 0;
            server.Method = (auth, input) =>
            {
                receivedCount = input.Count;
                return new HttpServer.Response();
            };
            Thread.Sleep(1100); // wait for more than 1 second, the timer should
            // flush all events            
            Assert.True(receivedCount == 10);
        }

        private void Sleep()
        {
            // logger and server are async thus we need short delays between individual tests
            Thread.Sleep(100); 
        }
    }
}
