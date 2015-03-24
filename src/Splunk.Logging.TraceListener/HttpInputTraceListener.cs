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

namespace Splunk.Logging
{
    /// <summary>
    /// Trace listener implementation for Splunk http input. 
    /// Usage example:
    /// <code>
    /// var trace = new TraceSource("logger");
    /// trace.listeners.Add(new HttpInputTraceListener(
    ///     uri: "https://localhost:8089", 
    ///     token: "E6099437-3E1F-4793-90AB-0E5D9438A918");
    /// trace.TraceEvent(TraceEventType.Information, 1, "hello world");
    /// </code>
    /// 
    /// Trace listener supports events batching (off by default) that allows to 
    /// decrease number of http requests to Splunk server. The batching is 
    /// controlled by three parameters: "batch size count", "batch size bytes" 
    /// and "batch interval". If batch size parameters are specified then  
    /// Send(...) adds logging events into an internal data buffer and multiple events
    /// are sending simultaneously when data buffer exceeds batching parameters.
    /// Batch interval controls a timer that forcefully sends events batch 
    /// regardless of its size.
    /// <code>
    /// var trace = new TraceSource("logger");
    /// trace.listeners.Add(new HttpInputTraceListener(
    ///     uri: "https://localhost:8089", 
    ///     token: "E6099437-3E1F-4793-90AB-0E5D9438A918",
    ///     batchInterval: 1000, // send events at least every second
    ///     batchSizeBytes: 1024, // 1KB
    ///     batchSizeCount: 10 // events batch contains at most 10 individual events
    /// );
    /// trace.TraceEvent(TraceEventType.Information, 1, "hello batching");
    /// </code> 
    /// 
    /// Trace listener allows recovering from transient connectivity problems 
    /// and it is controlled by messageHandler parameter. HttpInputResendMessageHandler 
    /// implements http handler that resends data multiple times.
    /// 
    /// <code>
    /// trace.listeners.Add(new HttpInputTraceListener(
    ///     uri: "https://localhost:8089", 
    ///     token: "E6099437-3E1F-4793-90AB-0E5D9438A918",
    ///     new HttpInputResendMessageHandler(100) // retry up to 10 times
    /// );
    /// </code>
    /// 
    /// A user application code can register an error handler that is invoked 
    /// when http input isn't able to send data. 
    /// <code>
    /// listener.AddLoggingFailureHandler((sender, HttpInputException e) =>
    /// {
    ///     // do something             
    /// });
    /// </code>
    /// HttpInputException contains information about the error and the list of 
    /// events caused the problem.
    /// </summary>
    public class HttpInputTraceListener : TraceListener
    {
        private HttpInputSender sender;

        /// <summary>
        /// HttpInputTraceListener c-or.
        /// </summary>
        /// <param name="uri">Splunk server uri, for example https://localhost:8089.</param>
        /// <param name="token">Http input authorization token.</param>
        /// <param name="metadata">Logger metadata.</param>
        /// <param name="batchInterval">Batch interval in milliseconds.</param>
        /// <param name="batchSizeBytes">Batch max size.</param>
        /// <param name="batchSizeCount">MNax number of individual events in batch.</param>
        /// <param name="messageHandler">Http message handler. By default 
        /// HttpInputResendMessageHandler with parameter 0 is used.</param>
        public HttpInputTraceListener(
            Uri uri, string token,
            Dictionary<string, string> metadata = null,
            uint batchInterval = 0, uint batchSizeBytes = 0, uint batchSizeCount = 0,
            HttpMessageHandler messageHandler = null)
        {
            sender = new HttpInputSender(
                uri, token, metadata,
                batchInterval, batchSizeBytes, batchSizeCount, messageHandler);
        }

        /// <summary>
        /// Add a handler to be invoked when some problem is detected during the 
        /// operation of http input and it cannot be fixed by resending the data.
        /// </summary>
        /// <param name="handler">A function to handle the exception.</param>
        public void AddLoggingFailureHandler(EventHandler<HttpInputException> handler)
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

        public override void Close()
        {
            sender.Flush();
        }
    }
}
