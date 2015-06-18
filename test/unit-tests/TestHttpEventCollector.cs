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
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;

namespace Splunk.Logging
{
    public class TestHttpEventCollector
    {
        private readonly Uri uri = new Uri("http://localhost:8089"); // a dummy uri
        private const string token = "TOKEN-GUID"; 
 
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

        private delegate Response RequestHandler(string token, List<HttpEventCollectorEventInfo> events);

        // we inject this method into HTTP event collector middleware chain to mimic a Splunk  
        // server
        private HttpEventCollectorSender.HttpEventCollectorMiddleware MiddlewareInterceptor(
            RequestHandler handler,
            HttpEventCollectorSender.HttpEventCollectorMiddleware middleware)
        {
            HttpEventCollectorSender.HttpEventCollectorMiddleware interceptor = 
            (string token, List<HttpEventCollectorEventInfo> events, HttpEventCollectorSender.HttpEventCollectorHandler next) =>
            {
                Response response = handler(token, events);
                HttpResponseMessage httpResponseMessage = new HttpResponseMessage();
                httpResponseMessage.StatusCode = response.Code;
                byte[] buf = Encoding.UTF8.GetBytes(response.Context);
                httpResponseMessage.Content = new StringContent(response.Context);                
                var task = new Task<HttpResponseMessage>(() => {
                    return httpResponseMessage; 
                });
                task.RunSynchronously();
                return task;
            };
            if (middleware != null)
            {
                // chain middleware to interceptor
                var temp = interceptor;
                interceptor = (token, events, next) =>
                {
                    return middleware(token, events, (t, e) =>
                    {
                        return temp(t, e, next);
                    });
                };
            }
            return interceptor;
        }

        // Input trace listener
        private TraceSource Trace(
            RequestHandler handler, 
            HttpEventCollectorEventInfo.Metadata metadata = null,
            int batchInterval = 0, 
            int batchSizeBytes = 0, 
            int batchSizeCount = 0,
            HttpEventCollectorSender.HttpEventCollectorMiddleware middleware = null)
        {
            var trace = new TraceSource("HttpEventCollectorLogger");
            trace.Switch.Level = SourceLevels.All;
            trace.Listeners.Add(
                new HttpEventCollectorTraceListener(
                    uri: uri,
                    token: token,
                    metadata: metadata,
                    batchInterval: batchInterval, 
                    batchSizeBytes: batchSizeBytes, 
                    batchSizeCount: batchSizeCount,
                    middleware: MiddlewareInterceptor(handler, middleware))
            );            
            return trace;
        }

        // Event sink
        private struct SinkTrace : IDisposable
        {
            public TestEventSource Source { set; get; }
            public HttpEventCollectorSink Sink { set; get; }
            public ObservableEventListener Listener { get; set; }
            public void Dispose()
            {
                Sink.OnCompleted();
                Listener.Dispose();
            }
        }

        private SinkTrace TraceSource(
            RequestHandler handler,
            HttpEventCollectorEventInfo.Metadata metadata = null,
            int batchInterval = 0,
            int batchSizeBytes = 0,
            int batchSizeCount = 0,
            HttpEventCollectorSender.HttpEventCollectorMiddleware middleware = null)
        {
            var listener = new ObservableEventListener();
            var sink = new HttpEventCollectorSink(
                 uri: uri,
                 token: token,
                 formatter: new TestEventFormatter(),
                 metadata: metadata,
                 batchInterval: batchInterval,
                 batchSizeBytes: batchSizeBytes,
                 batchSizeCount: batchSizeCount,
                 middleware: MiddlewareInterceptor(handler, middleware));
            listener.Subscribe(sink);

            var eventSource = TestEventSource.GetInstance();
            listener.EnableEvents(eventSource, EventLevel.LogAlways, Keywords.All);           
            return new SinkTrace() { 
                Source = eventSource,
                Sink = sink,
                Listener = listener
            };
        }

        #endregion

        [Trait("integration-tests", "Splunk.Logging.HttpEventCollectorCoreTest")]
        [Fact]
        public void HttpEventCollectorCoreTest()
        {
            // authorization
            var trace = Trace((token, input) =>
            {
                Assert.True(token == "TOKEN-GUID", "authentication");
                return new Response();
            });
            trace.TraceInformation("info");
            trace.Close();

            // metadata
            ulong now = (ulong)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            var metadata = new HttpEventCollectorEventInfo.Metadata(
                index: "main",
                source: "localhost",
                sourceType: "log",
                host: "demohost"
            );
            trace = Trace(
                metadata: metadata,
                handler: (token, events) =>
                {
                    Assert.True(events[0].Index == "main");
                    Assert.True(events[0].Source == "localhost");
                    Assert.True(events[0].SourceType == "log");
                    Assert.True(events[0].Host == "demohost");
                    // check that timestamp is correct
                    ulong time = ulong.Parse(events[0].Timestamp);
                    Assert.True(time - now < 10); // it cannot be more than 10s after sending event
                    return new Response();
                }
            );
            trace.TraceInformation("info");
            trace.Close();

            // test various tracing commands
            trace = Trace((token, events) =>
            {
                Assert.True(events[0].Event.Message == "info");
                return new Response();
            });
            trace.TraceInformation("info");
            trace.Close();

            trace = Trace((token, events) =>
            {
                Assert.True(events[0].Event.Severity == "Information");
                Assert.True(events[0].Event.Id == "1");
                Assert.True(((string[])(events[0].Event.Data))[0] == "one");
                Assert.True(((string[])(events[0].Event.Data))[1] == "two");
                return new Response();
            });
            trace.TraceData(TraceEventType.Information, 1, new string[] { "one", "two" });
            trace.Close();

            trace = Trace((token, events) =>
            {
                Assert.True(events[0].Event.Severity == "Critical");
                Assert.True(events[0].Event.Id == "2");
                return new Response();
            });
            trace.TraceEvent(TraceEventType.Critical, 2);
            trace.Close();

            trace = Trace((token, events) =>
            {
                Assert.True(events[0].Event.Severity == "Error");
                Assert.True(events[0].Event.Id == "3");
                Assert.True(events[0].Event.Message == "hello");
                return new Response();
            });
            trace.TraceEvent(TraceEventType.Error, 3, "hello");
            trace.Close();

            trace = Trace((token, events) =>
            {
                Assert.True(events[0].Event.Severity == "Resume");
                Assert.True(events[0].Event.Id == "4");
                Assert.True(events[0].Event.Message == "hello world");
                return new Response();
            });
            trace.TraceEvent(TraceEventType.Resume, 4, "hello {0}", "world");
            trace.Close();

            Guid guid = new Guid("11111111-2222-3333-4444-555555555555");            
            trace = Trace((token, events) =>
            {
                Assert.True(events[0].Event.Id == "5");
                Assert.True(((Guid)(events[0].Event.Data)).CompareTo(guid) == 0);
                return new Response();
            });
            trace.TraceTransfer(5, "transfer", guid);
            trace.Close();
        }

        [Trait("integration-tests", "Splunk.Logging.HttpEventCollectorBatchingCountTest")]
        [Fact]
        public void HttpEventCollectorBatchingCountTest()
        {
            var trace = Trace(
                handler: (token, events) =>
                {
                    Assert.True(events.Count == 3);
                    Assert.True(events[0].Event.Message == "info 1");
                    Assert.True(events[1].Event.Message == "info 2");
                    Assert.True(events[2].Event.Message == "info 3");
                    return new Response();
                },
                batchSizeCount: 3
            );            

            trace.TraceInformation("info 1");
            trace.TraceInformation("info 2");
            trace.TraceInformation("info 3");

            trace.TraceInformation("info 1");
            trace.TraceInformation("info 2");
            trace.TraceInformation("info 3");            

            trace.Close();
        }

        [Trait("integration-tests", "Splunk.Logging.HttpEventCollectorBatchingSizeTest")]
        [Fact]
        public void HttpEventCollectorBatchingSizeTest()
        {
            // estimate serialized event size
            HttpEventCollectorEventInfo ei = 
                new HttpEventCollectorEventInfo(null, TraceEventType.Information.ToString(), "info ?", null, null);
            int size = HttpEventCollectorSender.SerializeEventInfo(ei).Length;

            var trace = Trace(
                handler: (token, events) =>
                {
                    Assert.True(events.Count == 4);
                    Assert.True(events[0].Event.Message == "info 1");
                    Assert.True(events[1].Event.Message == "info 2");
                    Assert.True(events[2].Event.Message == "info 3");
                    Assert.True(events[3].Event.Message == "info 4");
                    return new Response();
                },
                batchSizeBytes: 4 * size - size / 2 // 4 events trigger post  
            );

            trace.TraceInformation("info 1");
            trace.TraceInformation("info 2");
            trace.TraceInformation("info 3");
            trace.TraceInformation("info 4");

            trace.TraceInformation("info 1");
            trace.TraceInformation("info 2");
            trace.TraceInformation("info 3");
            trace.TraceInformation("info 4");

            trace.Close();
        }

        [Trait("integration-tests", "Splunk.Logging.HttpEventCollectorBatchingIntervalTest")]
        [Fact]
        public void HttpEventCollectorBatchingIntervalTest()
        {
            var trace = Trace(
                handler: (token, events) =>
                {
                    Assert.True(events.Count == 4);
                    Assert.True(events[0].Event.Message == "info 1");
                    Assert.True(events[1].Event.Message == "info 2");
                    Assert.True(events[2].Event.Message == "info 3");
                    Assert.True(events[3].Event.Message == "info 4");
                    return new Response();
                },
                batchInterval: 1000
            );
            trace.TraceInformation("info 1");
            trace.TraceInformation("info 2");
            trace.TraceInformation("info 3");
            trace.TraceInformation("info 4");            
            trace.Close();
        }

        [Trait("integration-tests", "Splunk.Logging.HttpEventCollectorResendTest")]
        [Fact]
        public void HttpEventCollectorResendTest()
        {
            int resendCount = 0;
            HttpEventCollectorResendMiddleware resend = new HttpEventCollectorResendMiddleware(3);
            var trace = Trace(
                handler: (auth, input) =>
                {
                    resendCount++;
                    // mimic server error, this problem is considered as "fixable"
                    // by resend middleware
                    return new Response(HttpStatusCode.InternalServerError, "{\"text\":\"Error\"}");
                }, 
                middleware: (new HttpEventCollectorResendMiddleware(3)).Plugin // repeat 3 times
            );
            (trace.Listeners[trace.Listeners.Count-1] as HttpEventCollectorTraceListener).AddLoggingFailureHandler(
                (sender, exception) =>
                {
                    // error handler should be called after a single "normal post" and 3 "retries"
                    Assert.True(resendCount == 4);
                    
                    // check exception events
                    Assert.True(exception.Events.Count == 1);
                    Assert.True(exception.Events[0].Event.Message == "info");
                });
            trace.TraceInformation("info");
            trace.Close();
        }

        [Trait("integration-tests", "Splunk.Logging.HttpEventCollectorSinkCoreTest")]
        [Fact]
        public void HttpEventCollectorSinkCoreTest()
        {
            // authorization
            var trace = TraceSource((token, events) =>
            {
                Assert.True(token == "TOKEN-GUID", "authentication");
                return new Response();
            });
            trace.Source.Message("", "");
            trace.Dispose();

            // metadata
            var metadata = new HttpEventCollectorEventInfo.Metadata(
                index: "main",
                source: "localhost",
                sourceType: "log",
                host: "demohost"
            );
            trace = TraceSource(
                metadata: metadata,
                handler: (token, events) =>
                {
                    Assert.True(events[0].Event.Message ==
                        "EventId=1 EventName=MessageInfo Level=Error \"FormattedMessage=world - hello\" \"message=hello\" \"caller=world\"\r\n");                
                    Assert.True(events[0].Index == "main");
                    Assert.True(events[0].Source == "localhost");
                    Assert.True(events[0].SourceType == "log");
                    Assert.True(events[0].Host == "demohost");
                    return new Response();
                }
            );
            trace.Source.Message("hello", "world");
            trace.Dispose();

            // timestamp
            // metadata
            ulong now = (ulong)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            trace = TraceSource(
                handler: (token, events) =>
                {
                    // check that timestamp is correct
                    ulong time = ulong.Parse(events[0].Timestamp);
                    Assert.True(time - now < 10); // it cannot be more than 10s after sending event
                    return new Response();
                }
            );
            trace.Source.Message("", "");
            trace.Dispose();
        }

        [Trait("integration-tests", "Splunk.Logging.HttpEventCollectorSinkBatchingTest")]
        [Fact]
        public void HttpEventCollectorSinkBatchingTest()
        {
            var trace = TraceSource((token, events) =>
            {
                Assert.True(events.Count == 3);
                Assert.True(events[0].Event.Message ==
                    "EventId=1 EventName=MessageInfo Level=Error \"FormattedMessage=one - 1\" \"message=1\" \"caller=one\"\r\n");
                Assert.True(events[1].Event.Message ==
                    "EventId=1 EventName=MessageInfo Level=Error \"FormattedMessage=two - 2\" \"message=2\" \"caller=two\"\r\n");
                Assert.True(events[2].Event.Message ==
                    "EventId=1 EventName=MessageInfo Level=Error \"FormattedMessage=three - 3\" \"message=3\" \"caller=three\"\r\n");
                return new Response();
            },
            batchSizeCount: 3);
            
            trace.Source.Message("1", "one");
            trace.Source.Message("2", "two");
            trace.Source.Message("3", "three");

            trace.Source.Message("1", "one");
            trace.Source.Message("2", "two");
            trace.Source.Message("3", "three");

            trace.Dispose();
        }
    }
}
