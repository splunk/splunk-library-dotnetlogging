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
        #region Trace listener interceptor that replaces a real Splunk server for testing. 

        private class Response
        {
            public HttpStatusCode Code;
            public string Context;
            public Response(HttpStatusCode code = HttpStatusCode.OK, string context = "{\"text\":\"Success\",\"code\":0}")
            {
                Code = code;
                Context = context;
            }
        }

        private delegate Response RequestHandler(string auth, dynamic input); 

        private TraceSource Trace(
            RequestHandler handler, 
            HttpInputEventInfo.Metadata metadata = null,
            int batchInterval = 0, 
            int batchSizeBytes = 0, 
            int batchSizeCount = 0,
            HttpInputSender.HttpInputMiddleware middleware = null)
        {

            HttpInputSender.HttpInputMiddleware interceptor =
                async (HttpRequestMessage request, HttpInputSender.HttpInputHandler next) =>
                {
                    Response response = null;
                    HttpResponseMessage httpResponseMessage = new HttpResponseMessage();
                    try
                    {
                        string authorization = request.Headers.Authorization.ToString();
                        string input = await request.Content.ReadAsStringAsync();
                        if (input.Contains("}{"))
                        {
                            // batch of events, convert it to a json array
                            input = "[" + input.Replace("}{", "},{") + "]";
                            response = handler(authorization, JArray.Parse(input));
                        }
                        else
                        {
                            response = handler(authorization, JObject.Parse(input));
                        }
                        httpResponseMessage.StatusCode = response.Code;
                        byte[] buf = Encoding.UTF8.GetBytes(response.Context);
                        httpResponseMessage.Content = new StringContent(response.Context);
                    }
                    catch (Exception) { }
                    /*
                    if (httpResponseMessage.StatusCode != HttpStatusCode.OK)
                    {
                        throw new HttpInputException(
                            code: httpResponseMessage.StatusCode,
                            reply: response != null ? response.Context : null,
                            response: httpResponseMessage
                        );
                    }
                    */
                    return httpResponseMessage;
                };

            if (middleware != null)
            {
                // chain middleware to interceptor
                var temp = interceptor; 
                interceptor = (request, next) =>
                {
                    return middleware(request, (req) =>
                    {
                        return temp(req, next);
                    });
                };
            }

            var trace = new TraceSource("HttpInputLogger");
            trace.Switch.Level = SourceLevels.All;
            trace.Listeners.Add(
                new HttpInputTraceListener(
                    uri: new Uri("http://localhost:8089"), // dummy URI
                    token: "TOKEN-GUID", // dummy token 
                    metadata: metadata,
                    batchInterval: batchInterval, 
                    batchSizeBytes: batchSizeBytes, 
                    batchSizeCount: batchSizeCount,
                    middleware: interceptor)
            );
            
            return trace;
        }

        #endregion

        [Trait("integration-tests", "Splunk.Logging.HttpInputCoreTest")]
        [Fact]
        public void HttpInputCoreTest()
        {
            // authorization
            Trace((auth, input) =>
            {
                Assert.True(auth == "Splunk TOKEN-GUID", "authentication");
                return new Response();
            }).TraceInformation("info");

            // metadata
            ulong now = (ulong)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            var metadata = new HttpInputEventInfo.Metadata(
                index: "main",
                source: "localhost",
                sourceType: "log",
                host: "demohost"
            );
            Trace(
                metadata: metadata,
                handler: (auth, input) =>
                {
                    Assert.True(input.index.Value == "main");
                    Assert.True(input.source.Value == "localhost");
                    Assert.True(input.sourcetype.Value == "log");
                    Assert.True(input.host.Value == "demohost");
                    // check that timestamp is correct
                    ulong time = ulong.Parse(input.time.Value);
                    Assert.True(time - now < 10); // it cannot be more than 10s after sending event
                    return new Response();
                }
            ).TraceInformation("info");

            // test various tracing commands
            Trace((auth, input) =>
            {
                Assert.True(input["event"].message.Value == "info");
                return new Response();
            }).TraceInformation("info");

            Trace((auth, input) =>
            {
                Assert.True(input["event"].severity.Value == "Information");
                Assert.True(input["event"].id.Value == "1");
                Assert.True(input["event"].data[0].Value == "one");
                Assert.True(input["event"].data[1].Value == "two");
                return new Response();
            }).TraceData(TraceEventType.Information, 1,  new string[] { "one", "two" });

            Trace((auth, input) =>
            {
                Assert.True(input["event"].severity.Value == "Critical");
                Assert.True(input["event"].id.Value == "2");
                return new Response();
            }).TraceEvent(TraceEventType.Critical, 2);

            Trace((auth, input) =>
            {
                Assert.True(input["event"].severity.Value == "Error");
                Assert.True(input["event"].id.Value == "3");
                Assert.True(input["event"].message.Value == "hello");
                return new Response();
            }).TraceEvent(TraceEventType.Error, 3, "hello");

            Trace((auth, input) =>
            {
                Assert.True(input["event"].severity.Value == "Resume");
                Assert.True(input["event"].id.Value == "4");
                Assert.True(input["event"].message.Value == "hello world");
                return new Response();
            }).TraceEvent(TraceEventType.Resume, 4, "hello {0}", "world");

            string guid = "11111111-2222-3333-4444-555555555555";            
            Trace((auth, input) =>
            {
                Assert.True(input["event"].id.Value == "5");
                Assert.True(input["event"].data.Value == guid);
                return new Response();
            }).TraceTransfer(5, "transfer", new Guid(guid));

            // Wait for all async tasks before leaving the test. @todo - remove 
            // Sleep after SPL-98289 
            Sleep();
        }

        [Trait("integration-tests", "Splunk.Logging.HttpInputBatchingCountTest")]
        [Fact]
        public void HttpInputBatchingCountTest()
        {
            var trace = Trace(
                handler: (auth, input) =>
                {
                    Assert.True(input.Count == 3);
                    Assert.True(input[0]["event"].message.Value == "info 1");
                    Assert.True(input[1]["event"].message.Value == "info 2");
                    Assert.True(input[2]["event"].message.Value == "info 3");
                    return new Response();
                },
                batchSizeCount: 3
            );            
            trace.TraceInformation("info 1");
            trace.TraceInformation("info 2");
            trace.TraceInformation("info 3");

            Sleep();
        }

        [Trait("integration-tests", "Splunk.Logging.HttpInputBatchingSizeTest")]
        [Fact]
        public void HttpInputBatchingSizeTest()
        {
            // estimate serialized event size
            HttpInputEventInfo ei = 
                new HttpInputEventInfo(null, TraceEventType.Information.ToString(), "info ?", null, null);
            int size = HttpInputSender.SerializeEventInfo(ei).Length;

            var trace = Trace(
                handler: (auth, input) =>
                {
                    Assert.True(input.Count == 4);
                    Assert.True(input[0]["event"].message.Value == "info 1");
                    Assert.True(input[1]["event"].message.Value == "info 2");
                    Assert.True(input[2]["event"].message.Value == "info 3");
                    Assert.True(input[3]["event"].message.Value == "info 4");
                    return new Response();
                },
                batchSizeBytes: 4 * size - size / 2 // 4 events trigger post  
            );
            trace.TraceInformation("info 1");
            trace.TraceInformation("info 2");
            trace.TraceInformation("info 3");
            trace.TraceInformation("info 4");

            Sleep();
        }

        [Trait("integration-tests", "Splunk.Logging.HttpInputBatchingIntervalTest")]
        [Fact]
        public void HttpInputBatchingIntervalTest()
        {
            var trace = Trace(
                handler: (auth, input) =>
                {
                    Assert.True(input.Count == 4);
                    Assert.True(input[0]["event"].message.Value == "info 1");
                    Assert.True(input[1]["event"].message.Value == "info 2");
                    Assert.True(input[2]["event"].message.Value == "info 3");
                    Assert.True(input[3]["event"].message.Value == "info 4");
                    return new Response();
                },
                batchInterval: 1000,
                batchSizeBytes: int.MaxValue, batchSizeCount: int.MaxValue
            );
            trace.TraceInformation("info 1");
            trace.TraceInformation("info 2");
            trace.TraceInformation("info 3");
            trace.TraceInformation("info 4");

            Sleep();
        }

        [Trait("integration-tests", "Splunk.Logging.HttpInputResendTest")]
        [Fact]
        public void HttpInputResendTest()
        {
            int resendCount = 0;
            HttpInputResendMiddleware resend = new HttpInputResendMiddleware(3);
            var trace = Trace(
                handler: (auth, input) =>
                {
                    resendCount++;
                    // mimic server error, this problem is considered as "fixable"
                    // by resend middleware
                    return new Response(HttpStatusCode.InternalServerError, "{\"text\":\"Error\"}");
                }, 
                middleware: (new HttpInputResendMiddleware(3)).Plugin // repeat 3 times
            );
            (trace.Listeners[trace.Listeners.Count-1] as HttpInputTraceListener).AddLoggingFailureHandler(
                (sender, exception) =>
                {
                    // error handler should be called after a single "normal post" and 3 "retries"
                    Assert.True(resendCount == 4);
                    
                    // check exception events
                    Assert.True(exception.Events.Count == 1);
                    Assert.True(exception.Events[0].Event.Message == "info");
                });
            trace.TraceInformation("info"); 

            Sleep();
        }

        private void Sleep()
        {
            // Currently we put a sleep at the end of each test int order to make sure
            // that process doesn't exit before completing all async tests. This 
            // sleep will be removed (@todo!) after  SPL-98289
            Thread.Sleep(3000); 
        }
    }
}
