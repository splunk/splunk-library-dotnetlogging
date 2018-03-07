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
            HttpEventCollectorSender.SendMode sendMode = HttpEventCollectorSender.SendMode.Parallel,
            HttpEventCollectorSender.HttpEventCollectorMiddleware middleware = null)
        {
            var trace = new TraceSource("HttpEventCollectorLogger");
            trace.Switch.Level = SourceLevels.All;
            trace.Listeners.Add(
                new HttpEventCollectorTraceListener(
                    uri: uri,
                    token: token,
                    metadata: metadata,
                    sendMode: sendMode,
                    batchInterval: batchInterval,
                    batchSizeBytes: batchSizeBytes,
                    batchSizeCount: batchSizeCount,
                    middleware: MiddlewareInterceptor(handler, middleware))
            );
            return trace;
        }

        private TraceSource TraceDefault(RequestHandler handler)
        {
            var trace = new TraceSource("HttpEventCollectorLogger");
            trace.Switch.Level = SourceLevels.All;
            trace.Listeners.Add(
                new HttpEventCollectorTraceListener(
                    uri: uri,
                    token: token,
                    middleware: MiddlewareInterceptor(handler, null))
            );
            return trace;
        }

        private TraceSource TraceCustomFormatter(
            RequestHandler handler,
            HttpEventCollectorSender.HttpEventCollectorFormatter formatter,
            HttpEventCollectorSender.HttpEventCollectorMiddleware middleware)
        {
            var trace = new TraceSource("HttpEventCollectorLogger");
            trace.Switch.Level = SourceLevels.All;
            trace.Listeners.Add(
                new HttpEventCollectorTraceListener(
                    uri: uri,
                    token: token,
                    middleware: middleware,
                    formatter: formatter,
                    sendMode: HttpEventCollectorSender.SendMode.Parallel)
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
            HttpEventCollectorSender.SendMode sendMode = HttpEventCollectorSender.SendMode.Parallel,
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
                 sendMode: sendMode,
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
            double now = (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
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
                    double time = double.Parse(events[0].Timestamp);
                    Assert.True(time - now < 10.0); // it cannot be more than 10s after sending event
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

        [Trait("integration-tests", "Splunk.Logging.HttpEventCollectorSerializationTest")]
        [Fact]
        public void HttpEventCollectorSerializationTest()
        {
            Func<String, List<HttpEventCollectorEventInfo>, Response> noopHandler = (token, events) =>
            {
                return new Response();
            };

            int numFormattedEvents = 0;
            var middlewareEvents = new List<HttpEventCollectorEventInfo>();

            var trace = TraceCustomFormatter(
                handler: (token, events) => {
                    Assert.Equal(events.Count, 2);
                    Assert.Equal(events[0].Event.Mesage, "hello");
                    return new Response();
                },
                formatter: (eventInfo) => {
                    numFormattedEvents++;
                    var ev = eventInfo.Event;
                    switch (numFormattedEvents)
                    {
                        case 1:
                            ev = ev.Message;
                            break;
                        case 2:
                            ev = new {
                                newProperty = "hello world!",
                                Id = eventInfo.Event.Id,
                                Message = eventInfo.Event.Message,
                                Data = eventInfo.Event.Data,
                                Severity = eventInfo.Event.Severity
                            };
                            break;
                        case 3:
                            string[] fieldArray = { "that", "I", "want" };
                            ev = new {
                                i = "can",
                                use = "any",
                                fields = fieldArray,
                                seriously = true,
                                num = 99.9
                            };
                            break;
                    }
                    return ev;
                },
                middleware: new HttpEventCollectorSender.HttpEventCollectorMiddleware((token, events, next) => {
                    middlewareEvents = events;

                    Response response = noopHandler(token, events);
                    HttpResponseMessage httpResponseMessage = new HttpResponseMessage();
                    httpResponseMessage.StatusCode = response.Code;
                    byte[] buf = Encoding.UTF8.GetBytes(response.Context);
                    httpResponseMessage.Content = new StringContent(response.Context);
                    var task = new Task<HttpResponseMessage>(() => {
                        return httpResponseMessage;
                    });
                    task.RunSynchronously();
                    return task;
                })
            );

            trace.TraceInformation("hello");
            trace.TraceInformation("hello2");
            trace.TraceInformation("hello3");

            (trace.Listeners[trace.Listeners.Count - 1] as HttpEventCollectorTraceListener).FlushAsync().RunSynchronously();
            trace.Close();

            Assert.Equal(numFormattedEvents, 3);
            Assert.Equal(middlewareEvents.Count, 3);

            Assert.Equal(middlewareEvents[0].Event, "hello");

            Assert.Equal(middlewareEvents[1].Event.Message, "hello2");
            Assert.Equal(middlewareEvents[1].Event.Severity, "Information");
            Assert.Equal(middlewareEvents[1].Event.Id, "0"); // Defaults to "0"
            Assert.Equal(middlewareEvents[1].Event.newProperty, "hello world!");

            Assert.Equal(middlewareEvents[2].Event.i, "can");
            Assert.Equal(middlewareEvents[2].Event.use, "any");
            Assert.Equal(middlewareEvents[2].Event.fields.Length, 3);
            string[] expectedFields = { "that", "I", "want" };
            Assert.Equal(middlewareEvents[2].Event.fields, expectedFields);
            Assert.Equal(middlewareEvents[2].Event.seriously, true);
            Assert.Equal(middlewareEvents[2].Event.num, 99.9);
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
                (exception) =>
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
            double now = (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;            
            trace = TraceSource(
                handler: (token, events) =>
                {
                    // check that timestamp is correct
                    double time = double.Parse(events[0].Timestamp);
                    Assert.True(time - now < 10.0); // it cannot be more than 10s after sending event
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

        [Trait("integration-tests", "Splunk.Logging.HttpEventCollectorAsyncFlushTest")]
        [Fact]
        public void HttpEventCollectorAsyncFlushTest()
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
                batchInterval: 10000
            );
            trace.TraceInformation("info 1");
            trace.TraceInformation("info 2");
            trace.TraceInformation("info 3");
            trace.TraceInformation("info 4");
            HttpEventCollectorTraceListener listener = trace.Listeners[1] as HttpEventCollectorTraceListener;
            listener.FlushAsync().RunSynchronously();
        }

        [Trait("integration-tests", "Splunk.Logging.HttpEventCollectorSeqModeTest")]
        [Fact]
        public void HttpEventCollectorSeqModeTest()
        {
            int expected = 0;
            var trace = Trace(
                handler: (token, events) =>
                {
                    Assert.True(events.Count == 1);
                    Assert.True(int.Parse(events[0].Event.Message) == expected);
                    expected++;
                    return new Response();
                },
                sendMode: HttpEventCollectorSender.SendMode.Sequential
            );
            for (int n = 0; n < 100; n++)
            trace.TraceInformation(n.ToString());
            trace.Close();
        }

        [Trait("integration-tests", "Splunk.Logging.HttpEventCollectorSeqModeWithBatchTest")]
        [Fact]
        public void HttpEventCollectorSeqModeWithBatchTest()
        {
            int expected = 0;
            var trace = Trace(
                handler: (token, events) =>
                {
                    Assert.True(events.Count == 2);
                    Assert.True(int.Parse(events[0].Event.Message) == expected);
                    Assert.True(int.Parse(events[1].Event.Message) == expected + 1);
                    expected += 2;
                    return new Response();
                },
                sendMode: HttpEventCollectorSender.SendMode.Sequential,
                batchSizeCount: 2
            );
            for (int n = 0; n < 100; n++)
                trace.TraceInformation(n.ToString());
            trace.Close();
        }

        [Trait("integration-tests", "Splunk.Logging.HttpEventCollectorDefaultSettingsCountTest")]
        [Fact]
        public void HttpEventCollectorDefaultSettingsCountTest()
        {
            var trace = TraceDefault(
                handler: (token, events) =>
                {
                    Assert.True(events.Count == HttpEventCollectorSender.DefaultBatchCount);
                    return new Response();
                }
            );
            for (int n = 0; n < HttpEventCollectorSender.DefaultBatchCount * 10; n++)
                trace.TraceInformation(n.ToString());
            trace.Close();
        }

        [Trait("integration-tests", "Splunk.Logging.HttpEventCollectorDefaultSettingsSizeTest")]
        [Fact]
        public void HttpEventCollectorDefaultSettingsSizeTest()
        {
            var trace = TraceDefault(
                handler: (token, events) =>
                {
                    Assert.True(events.Count == 1);
                    return new Response();
                }
            );
            for (int n = 0; n < 10; n++)
            {
                trace.TraceInformation(new String('*', HttpEventCollectorSender.DefaultBatchSize));
            }
            trace.Close();
        }

        [Trait("integration-tests", "Splunk.Logging.HttpEventCollectorDefaultSettingsIntervalTest")]
        [Fact]
        public void HttpEventCollectorDefaultSettingsIntervalTest()
        {
            bool eventReceived = false;
            var trace = TraceDefault(
                handler: (token, events) =>
                {
                    eventReceived = true;
                    return new Response();
                }
            );            
            trace.TraceInformation("=|:-)");
            Thread.Sleep(HttpEventCollectorSender.DefaultBatchInterval / 2);
            Assert.False(eventReceived);
            Thread.Sleep(HttpEventCollectorSender.DefaultBatchInterval);
            Assert.True(eventReceived);
        }

        [Trait("integration-tests", "Splunk.Logging.HttpEventCollectorEventInfoTimestampsTest")]
         [Fact]
         public void HttpEventCollectorEventInfoTimestampsTest()
         {
             // test setting timestamp
             DateTime utcNow = DateTime.UtcNow;
             double nowEpoch = (utcNow - new DateTime(1970, 1, 1)).TotalSeconds;
 
             HttpEventCollectorEventInfo ei =
             new HttpEventCollectorEventInfo(utcNow.AddHours(-1), null, null, null, null, null);
 
             double epochTimestamp = double.Parse(ei.Timestamp);
             double diff = Math.Ceiling(nowEpoch - epochTimestamp);
             Assert.True(diff >= 3600.0);
 
             // test default timestamp
             ei = new HttpEventCollectorEventInfo(null, null, null, null, null);
             utcNow = DateTime.UtcNow;
             nowEpoch = (utcNow - new DateTime(1970, 1, 1)).TotalSeconds;
             epochTimestamp = double.Parse(ei.Timestamp);
             diff = Math.Ceiling(nowEpoch - epochTimestamp);
             Assert.True(diff< 10.0);
         }
        
        [Trait("integration-tests", "Splunk.Logging.HttpEventCollectorSenderMetadataOverrideTest")]
        [Fact]
        public void HttpEventCollectorSenderMetadataOverrideTest()
        {
            Func<String, List<HttpEventCollectorEventInfo>, Response> noopHandler = (token, events) =>
            {
                return new Response();
            };
 
            HttpEventCollectorEventInfo.Metadata defaultmetadata = new HttpEventCollectorEventInfo.Metadata(index: "defaulttestindex", 
                source: "defaulttestsource", sourceType: "defaulttestsourcetype", host: "defaulttesthost");

            HttpEventCollectorEventInfo.Metadata overridemetadata = new HttpEventCollectorEventInfo.Metadata(index: "overridetestindex",
                  source: "overridetestsource", sourceType: "overridetestsourcetype", host: "overridetesthost");

            HttpEventCollectorSender httpEventCollectorSender =
                new HttpEventCollectorSender(uri, "TOKEN-GUID", 
                        defaultmetadata, 
                        HttpEventCollectorSender.SendMode.Sequential, 
                        100000, 
                        100000, 
                        3,
                        new HttpEventCollectorSender.HttpEventCollectorMiddleware((token, events, next) => {
                            Assert.True(events.Count == 3);

                            // Id = id1 should have the default meta data values.
                            Assert.True(events[0].Event.Id == "id1");
                            Assert.True(events[0].Index == defaultmetadata.Index);
                            Assert.True(events[0].Source == defaultmetadata.Source);
                            Assert.True(events[0].SourceType == defaultmetadata.SourceType);
                            Assert.True(events[0].Host == defaultmetadata.Host);

                            // Id = id2 should have the metadataOverride values.
                            Assert.True(events[1].Event.Id == "id2");
                            Assert.True(events[1].Index == overridemetadata.Index);
                            Assert.True(events[1].Source == overridemetadata.Source);
                            Assert.True(events[1].SourceType == overridemetadata.SourceType);
                            Assert.True(events[1].Host == overridemetadata.Host);

                            // Id = id3 should have the default meta data values.
                            Assert.True(events[2].Event.Id == "id3");
                            Assert.True(events[2].Index == defaultmetadata.Index);
                            Assert.True(events[2].Source == defaultmetadata.Source);
                            Assert.True(events[2].SourceType == defaultmetadata.SourceType);
                            Assert.True(events[2].Host == defaultmetadata.Host);

                            Response response = noopHandler(token, events);
                            HttpResponseMessage httpResponseMessage = new HttpResponseMessage();
                            httpResponseMessage.StatusCode = response.Code;
                            byte[] buf = Encoding.UTF8.GetBytes(response.Context);
                            httpResponseMessage.Content = new StringContent(response.Context);
                            var task = new Task<HttpResponseMessage>(() => {
                                return httpResponseMessage;
                            });
                            task.RunSynchronously();
                            return task;
                        })
                    );

            httpEventCollectorSender.Send(id: "id1");
            httpEventCollectorSender.Send(id: "id2", metadataOverride: overridemetadata);
            httpEventCollectorSender.Send(id: "id3");

            httpEventCollectorSender.FlushSync();
            httpEventCollectorSender.Dispose();
        }
    }
}
