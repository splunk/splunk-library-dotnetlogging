/**
 * @copyright
 *
 * Copyright 2013-2015 Splunk, Inc.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"): you may
 * not use this file except in compliance with the License. You may obtain
 * a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 */

using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Splunk.Logging
{
    /// <summary>
    /// Trace sink implementation for Splunk HTTP event collector. 
    /// Usage example:
    /// <code>
    /// var listener = new ObservableEventListener();
    /// var sink = new HttpEventCollectorEventSink(
    ///     uri: new Uri("https://localhost:8089"), 
    ///     token: "E6099437-3E1F-4793-90AB-0E5D9438A918",
    ///     formatter: new AppEventFormatter()
    /// );
    /// listener.Subscribe(sink);
    /// var eventSource = new AppEventSource();
    /// listener.EnableEvents(eventSource, EventLevel.LogAlways, Keywords.All);
    /// eventSource.Message("Hello world");
    /// </code>
    /// AppEventFormatter and AppEventSource have to be implemented by user. See
    /// TestHttpEventCollector.cs for a working example.
    /// 
    /// Trace sink supports events batching (off by default) that allows to 
    /// decrease number of HTTP requests to Splunk server. The batching is 
    /// controlled by three parameters: "batch size count", "batch size bytes" 
    /// and "batch interval". If batch size parameters are specified then  
    /// Send(...) multiple events are sending simultaneously batch exceeds its limits.
    /// Batch interval controls a timer that forcefully sends events batch 
    /// regardless of its size.
    /// <code>
    /// var listener = new ObservableEventListener();
    /// var sink = new HttpEventCollectorEventSink(
    ///     uri: new Uri("https://localhost:8089"), 
    ///     token: "E6099437-3E1F-4793-90AB-0E5D9438A918",
    ///     formatter: new AppEventFormatter(),
    ///     batchInterval: 1000, // send events at least every second
    ///     batchSizeBytes: 1024, // 1KB
    ///     batchSizeCount: 10) // events batch contains at most 10 individual events
    /// );
    /// listener.Subscribe(sink);
    /// var eventSource = new AppEventSource();
    /// listener.EnableEvents(eventSource, EventLevel.LogAlways, Keywords.All);
    /// eventSource.Message("Hello batching 1");
    /// eventSource.Message("Hello batching 2");
    /// eventSource.Message("Hello batching 3");
    /// </code> 
    /// 
    /// To improve system performance tracing events are sent asynchronously and
    /// events with the same timestamp (that has 1 millisecond resolution)  may 
    /// be indexed out of order by Splunk. sendMode parameter triggers
    /// "sequential mode" that guarantees preserving events order. In 
    /// "sequential mode" performance of sending events to the server is lower.
    /// 
    /// There is an ability to plug middleware components that act before and 
    /// after posting data.
    /// For example:
    /// <code>
    /// var sink = new HttpEventCollectorEventSink(
    ///     uri: new Uri("https://localhost:8089"), 
    ///     token: "E6099437-3E1F-4793-90AB-0E5D9438A918",
    ///     formatter: new AppEventFormatter(),
    ///     middleware: (request, next) => {
    ///         // preprocess request
    ///         var response = next(request); // post data
    ///         // process response
    ///         return response;
    ///     }
    ///     ...
    /// )
    /// </code>
    /// Middleware components can apply additional logic before and after posting
    /// the data to Splunk server. See HttpEventCollectorResendMiddleware.
    /// </remarks>
    /// 
    /// A user application code can register an error handler that is invoked 
    /// when HTTP event collector isn't able to send data. 
    /// <code>
    /// var sink = new HttpEventCollectorEventSink(
    ///     uri: new Uri("https://localhost:8089"), 
    ///     token: "E6099437-3E1F-4793-90AB-0E5D9438A918",
    ///     formatter: new AppEventFormatter(),
    /// );
    /// sink.AddLoggingFailureHandler((sender, HttpEventCollectorException e) =>
    /// {
    ///     // do something             
    /// });
    /// </code>
    /// HttpEventCollectorException contains information about the error and the list of 
    /// events caused the problem.
    /// </summary>
    public class HttpEventCollectorSink : IObserver<EventEntry>
    {
        private HttpEventCollectorSender sender;
        private IEventTextFormatter formatter;

        /// <summary>
        /// HttpEventCollectorEventSink c-or with middleware parameter.
        /// </summary>
        /// <param name="uri">Splunk server uri, for example https://localhost:8089.</param>
        /// <param name="token">HTTP event collector authorization token.</param>
        /// <param name="formatter">Event formatter converting EventEntry instance into a string.</param>
        /// <param name="metadata">Logger metadata.</param>
        /// <param name="sendMode">Send mode of the events.</param>
        /// <param name="batchInterval">Batch interval in milliseconds.</param>
        /// <param name="batchSizeBytes">Batch max size.</param>
        /// <param name="batchSizeCount">MNax number of individual events in batch.</param>
        /// <param name="middleware">
        /// HTTP client middleware. This allows to plug an HttpClient handler that 
        /// intercepts logging HTTP traffic.
        /// </param>
        public HttpEventCollectorSink(
            Uri uri, string token,
            IEventTextFormatter formatter,
            HttpEventCollectorEventInfo.Metadata metadata = null,
            HttpEventCollectorSender.SendMode sendMode = HttpEventCollectorSender.SendMode.Sequential,
            int batchInterval = HttpEventCollectorSender.DefaultBatchInterval, 
            int batchSizeBytes = HttpEventCollectorSender.DefaultBatchSize, 
            int batchSizeCount = HttpEventCollectorSender.DefaultBatchCount,
            HttpEventCollectorSender.HttpEventCollectorMiddleware middleware = null)
        {
            this.formatter = formatter;
            sender = new HttpEventCollectorSender(
                uri, token, metadata,
                sendMode,
                batchInterval, batchSizeBytes, batchSizeCount, 
                middleware);
        }

        /// <summary>
        /// HttpEventCollectorEventSink c-or. Instantiates HttpEventCollectorEventSink 
        /// when retriesOnError parameter is specified.
        /// </summary>
        /// <param name="uri">Splunk server uri, for example https://localhost:8089.</param>
        /// <param name="token">HTTP event collector authorization token.</param>
        /// <param name="formatter">Event formatter converting EventEntry instance into a string.</param>
        /// <param name="retriesOnError">Number of retries when network problem is detected</param> 
        /// <param name="metadata">Logger metadata.</param>
        /// <param name="sequentialMode">Send mode of the events.</param>
        /// <param name="batchInterval">Batch interval in milliseconds.</param>
        /// <param name="batchSizeBytes">Batch max size.</param>
        /// <param name="batchSizeCount">MNax number of individual events in batch.</param>        
        public HttpEventCollectorSink(
            Uri uri, string token,
            IEventTextFormatter formatter,
            int retriesOnError,
            HttpEventCollectorEventInfo.Metadata metadata = null,
            HttpEventCollectorSender.SendMode sendMode = HttpEventCollectorSender.SendMode.Sequential,
            int batchInterval = HttpEventCollectorSender.DefaultBatchInterval,
            int batchSizeBytes = HttpEventCollectorSender.DefaultBatchSize,
            int batchSizeCount = HttpEventCollectorSender.DefaultBatchCount)
            : this(uri, token, formatter, metadata, 
                   sendMode,
                   batchInterval, batchSizeBytes, batchSizeCount,
                   (new HttpEventCollectorResendMiddleware(retriesOnError)).Plugin)
        {
        }

        /// <summary>
        /// Add a handler to be invoked when some problem is detected during the 
        /// operation of HTTP event collector and it cannot be fixed by resending the data.
        /// </summary>
        /// <param name="handler">A function to handle the exception.</param>
        public void AddLoggingFailureHandler(Action<HttpEventCollectorException> handler)
        {
            sender.OnError += handler;
        }

        #region IObserver<EventEntry>

        public void OnCompleted()
        {
            sender.FlushSync();
        }

        public void OnError(Exception error)
        {
            sender.Dispose();
        }

        /// <summary>
        /// All events are going through OnNext callback.
        /// </summary>
        /// <param name="value">An event.</param>
        public void OnNext(EventEntry value)
        {
            using (var sw = new StringWriter())
            {
                formatter.WriteEvent(value, sw);
                sender.Send(
                    id: value.EventId.ToString(),
                    severity: value.Schema.Level.ToString(),
                    message: sw.ToString()
                );
            }
        }

        #endregion
    }
}
