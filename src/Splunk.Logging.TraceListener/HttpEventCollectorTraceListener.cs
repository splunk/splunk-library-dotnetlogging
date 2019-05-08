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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;

namespace Splunk.Logging
{
    /// <summary>
    /// Trace listener implementation for Splunk HTTP event collector. 
    /// Usage example:
    /// <code>
    /// var trace = new TraceSource("logger");
    /// trace.listeners.Add(new HttpEventCollectorTraceListener(
    ///     uri: new Uri("https://localhost:8088"), 
    ///     token: "E6099437-3E1F-4793-90AB-0E5D9438A918"));
    /// trace.TraceEvent(TraceEventType.Information, 1, "hello world");
    /// </code>
    /// 
    /// Trace listener supports events batching (off by default) that allows to 
    /// decrease number of HTTP requests to Splunk server. The batching is 
    /// controlled by three parameters: "batch size count", "batch size bytes" 
    /// and "batch interval". If batch size parameters are specified then  
    /// Send(...) multiple events are sending simultaneously batch exceeds its limits.
    /// Batch interval controls a timer that forcefully sends events batch 
    /// regardless of its size.
    /// <code>
    /// var trace = new TraceSource("logger");
    /// trace.listeners.Add(new HttpEventCollectorTraceListener(
    ///     uri: new Uri("https://localhost:8088"), 
    ///     token: "E6099437-3E1F-4793-90AB-0E5D9438A918",
    ///     batchInterval: 1000, // send events at least every second
    ///     batchSizeBytes: 1024, // 1KB
    ///     batchSizeCount: 10) // events batch contains at most 10 individual events
    /// );
    /// trace.TraceEvent(TraceEventType.Information, 1, "hello batching");
    /// </code> 
    ///
    /// To improve system performance tracing events are sent asynchronously and
    /// events with the same timestamp (that has 1 millisecond resolution) may 
    /// be indexed out of order by Splunk. sendMode parameter triggers
    /// "sequential mode" that guarantees preserving events order. In 
    /// "sequential mode" performance of sending events to the server is lower.
    ///  
    /// There is an ability to plug middleware components that act before and 
    /// after posting data.
    /// For example:
    /// <code>
    /// new HttpEventCollectorTraceListener(
    ///     uri: new Uri("https://localhost:8088"), 
    ///     token: "E6099437-3E1F-4793-90AB-0E5D9438A918,
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
    /// var listener = new HttpEventCollectorTraceListener(
    ///     uri: new Uri("https://localhost:8088"), 
    ///     token: "E6099437-3E1F-4793-90AB-0E5D9438A918")
    /// );
    /// listener.AddLoggingFailureHandler((sender, HttpEventCollectorException e) =>
    /// {
    ///     // do something             
    /// });
    /// trace.listeners.Add(listener);
    /// </code>
    /// HttpEventCollectorException contains information about the error and the list of 
    /// events caused the problem.
    /// </summary>
    public class HttpEventCollectorTraceListener : TraceListener
    {
        private HttpEventCollectorSender sender;
        public HttpEventCollectorSender.HttpEventCollectorFormatter formatter;

        /// <summary>
        /// HttpEventCollectorTraceListener c-or.
        /// </summary>
        /// <param name="uri">Splunk server uri, for example https://localhost:8088.</param>
        /// <param name="token">HTTP event collector authorization token.</param>
        /// <param name="metadata">Logger metadata.</param>
        /// <param name="sendMode">Send mode of the events.</param>
        /// <param name="batchInterval">Batch interval in milliseconds.</param>
        /// <param name="batchSizeBytes">Batch max size.</param>
        /// <param name="batchSizeCount">MNax number of individual events in batch.</param>
        /// <param name="middleware">
        /// HTTP client middleware. This allows to plug an HttpClient handler that 
        /// intercepts logging HTTP traffic.
        /// </param>
        public HttpEventCollectorTraceListener(
            Uri uri, string token,
            HttpEventCollectorEventInfo.Metadata metadata = null,
            HttpEventCollectorSender.SendMode sendMode = HttpEventCollectorSender.SendMode.Sequential,
            int batchInterval = HttpEventCollectorSender.DefaultBatchInterval,
            int batchSizeBytes = HttpEventCollectorSender.DefaultBatchSize,
            int batchSizeCount = HttpEventCollectorSender.DefaultBatchCount,
            HttpEventCollectorSender.HttpEventCollectorMiddleware middleware = null,
            HttpEventCollectorSender.HttpEventCollectorFormatter formatter = null)
        {
            this.formatter = formatter;
            sender = new HttpEventCollectorSender(
                uri, token, metadata,
                sendMode, 
                batchInterval, batchSizeBytes, batchSizeCount, 
                middleware,
                formatter);
        }

        /// <summary>
        /// HttpEventCollectorTraceListener c-or. Instantiates HttpEventCollectorTraceListener 
        /// when retriesOnError parameter is specified.
        /// </summary>
        /// <param name="uri">Splunk server uri, for example https://localhost:8088.</param>
        /// <param name="token">HTTP event collector authorization token.</param>
        /// <param name="retriesOnError">Number of retries when network problem is detected</param> 
        /// <param name="metadata">Logger metadata.</param>
        /// <param name="sendMode">Send mode of the events.</param>
        /// <param name="batchInterval">Batch interval in milliseconds.</param>
        /// <param name="batchSizeBytes">Batch max size.</param>
        /// <param name="batchSizeCount">MNax number of individual events in batch.</param>        
        public HttpEventCollectorTraceListener(
            Uri uri, string token,
            int retriesOnError,
            HttpEventCollectorEventInfo.Metadata metadata = null,
            HttpEventCollectorSender.SendMode sendMode = HttpEventCollectorSender.SendMode.Sequential,
            int batchInterval = HttpEventCollectorSender.DefaultBatchInterval,
            int batchSizeBytes = HttpEventCollectorSender.DefaultBatchSize,
            int batchSizeCount = HttpEventCollectorSender.DefaultBatchCount)
            : this(uri, token, metadata, 
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

        #region TraceListener output callbacks
        
        public override void Write(string message) 
        {
            sender.Send(message: message);
        }

        public override void WriteLine(string message) 
        {
            sender.Send(message: message);
        }

        public override void TraceData(
            TraceEventCache eventCache,
            string source,
            TraceEventType eventType,
            int id,
            object data)
        {
            sender.Send(
                id: id.ToString(),
                severity: eventType.ToString(),
                data: data
            );
        }

        public override void TraceData(
            TraceEventCache eventCache, 
            string source, 
            TraceEventType eventType, 
            int id, 
            params object[] data)
        {
            sender.Send(
                id: id.ToString(), 
                severity: eventType.ToString(),
                data: data
            );
        }

        public override void TraceEvent(
            TraceEventCache eventCache, 
            string source, 
            TraceEventType eventType, 
            int id)
        {
            sender.Send(
                id: id.ToString(),
                severity: eventType.ToString()
            );
        }

        public override void TraceEvent(
            TraceEventCache eventCache, 
            string source, 
            TraceEventType eventType, 
            int id, 
            string message)
        {
            sender.Send(
                id: id.ToString(), 
                severity: eventType.ToString(), 
                message: message
            );
        }

        public override void TraceEvent(
            TraceEventCache eventCache, 
            string source, 
            TraceEventType eventType, 
            int id, 
            string format, 
            params object[] args)
        {
            string message = args != null ? string.Format(CultureInfo.InvariantCulture, format, args) : format;
            sender.Send(
                id: id.ToString(),
                severity: eventType.ToString(),
                message: message
            );
        }

        public override void TraceTransfer(
            TraceEventCache eventCache, 
            string source, 
            int id, 
            string message, 
            Guid relatedActivityId)
        {
            sender.Send(
                id: id.ToString(),
                message: message,
                data: relatedActivityId
            );
        }

        #endregion

        /// <summary>
        /// Flush all events.
        /// </summary>
        public Task FlushAsync()
        {
            return sender.FlushAsync();
        }

        public override void Close()
        {
            sender.FlushSync();
        }

        override protected void Dispose(bool disposing)
        {
            Close();
        }

        ~HttpEventCollectorTraceListener()
        {
            Dispose(false);
        }
    }
}
