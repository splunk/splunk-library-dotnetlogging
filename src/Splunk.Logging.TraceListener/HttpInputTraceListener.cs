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

namespace Splunk.Logging
{
    /// <summary>
    /// Trace listener implementation for Splunk http input. 
    /// Usage example:
    /// var trace = new TraceSource("logger");
    /// trace.listeners.Add(new HttpInputTraceListener(
    ///     uri: "https://localhost:8089", 
    ///     token: "E6099437-3E1F-4793-90AB-0E5D9438A918");
    /// trace.TraceEvent(TraceEventType.Information, 1, "hello world");
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
        /// <param name="retriesOnError">Number of retries in case of connectivity problem.</param>
        public HttpInputTraceListener(
            string uri, string token,
            Dictionary<string, string> metadata = null,
            uint batchInterval = 0, uint batchSizeBytes = 0, uint batchSizeCount = 0,
            uint retriesOnError = 0)
        {
            sender = new HttpInputSender(
                uri, token, metadata,
                batchInterval, batchSizeBytes, batchSizeCount, retriesOnError);
        }

        /// <summary>
        /// @TODO - error handling
        /// </summary>
        /// <param name="handler"></param>
        public void AddLoggingFailureHandler(Action<Exception> handler)
        {
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

        public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id, params object[] data)
        {
            sender.Send(
                id: id.ToString(), 
                severity: eventType.ToString(),
                data: data
            );
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id)
        {
            sender.Send(
                id: id.ToString(),
                severity: eventType.ToString()
            );
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            sender.Send(
                id: id.ToString(), 
                severity: eventType.ToString(), 
                message: message
            );
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
        {
            string message = args != null ? string.Format(format, args) : format;
            sender.Send(
                id: id.ToString(),
                severity: eventType.ToString(),
                message: message
            );
        }

        public override void TraceTransfer(TraceEventCache eventCache, string source, int id, string message, Guid relatedActivityId)
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
