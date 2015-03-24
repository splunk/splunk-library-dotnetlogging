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
using System.Net.Http;
using System.Text;
using System.Net;
using Newtonsoft.Json;
using System.Threading;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Splunk.Logging
{
    /// <summary>
    /// Http input client side implementation that collects, serializes and send 
    /// events to Splunk http input endpoint. This class shouldn't be used directly
    /// by user applications.
    /// </summary>
    /// <remarks>
    /// * HttpInputSender is thread safe and Send(...) method may be called from
    /// different threads.
    /// * Events are are sending asynchronously and Send(...) method doesn't 
    /// block the caller code.
    /// </remarks>
    public class HttpInputSender : IDisposable
    {
        private const string HttpInputPath = "/services/receivers/token";
        private const string AuthorizationHeaderScheme = "Splunk";
        private Uri httpInputEndpointUri; // http input endpoint full uri
        private Dictionary<string, string> metadata; // logger metadata

        // events batching properties and collection 
        uint batchInterval = 0; 
        uint batchSizeBytes = 0;
        uint batchSizeCount = 0;
        HttpClient httpClient = null;
        private List<HttpInputEventInfo> eventsBatch = new List<HttpInputEventInfo>();
        private StringBuilder serializedEventsBatch = new StringBuilder();
        private Timer timer;

        public event EventHandler<HttpInputException> OnError = (s, e)=>{};


        /// <summary>
        /// HttpInputSender c-or.
        /// </summary>
        /// <param name="uri">Splunk server uri, for example https://localhost:8089.</param>
        /// <param name="token">Http input authorization token.</param>
        /// <param name="metadata">Logger metadata.</param>
        /// <param name="batchInterval">Batch interval in milliseconds.</param>
        /// <param name="batchSizeBytes">Batch max size.</param>
        /// <param name="batchSizeCount">MNax number of individual events in batch.</param>
        /// <param name="messageHandler">Http messages client.</param>
        public HttpInputSender(
            Uri uri, string token, Dictionary<string, string> metadata,
            uint batchInterval, uint batchSizeBytes, uint batchSizeCount, 
            HttpMessageHandler messageHandler)
        {
            this.httpInputEndpointUri = new Uri(uri, HttpInputPath);
            this.batchInterval = batchInterval;
            this.batchSizeBytes = batchSizeBytes;
            this.batchSizeCount = batchSizeCount;
            this.metadata = metadata;

            // when size configuration setting is missing it's treated as "infinity",
            // i.e., any value is accepted.
            if (this.batchSizeCount == 0 && this.batchSizeBytes > 0)
            {
                this.batchSizeCount = uint.MaxValue;
            }
            else if (this.batchSizeBytes == 0 && this.batchSizeCount > 0)
            {
                this.batchSizeBytes = uint.MaxValue;
            }

            // setup the timer
            if (batchInterval != 0) // 0 means - no timer
            {
                timer = new Timer(OnTimer, null, (int)batchInterval, (int)batchInterval);        
            }

            // setup http client            
            if (messageHandler == null)
            {
                // by default we use message handler without resend
                messageHandler = new HttpInputResendMessageHandler(0);
            }
            httpClient = new HttpClient(messageHandler);
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(AuthorizationHeaderScheme, token);
        }

        /// <summary>
        /// Send an event to Splunk http endpoint. Actual event send is done 
        /// asynchronously and this method doesn't block client application.
        /// </summary>
        /// <param name="id">Event id.</param>
        /// <param name="severity">Event severity info.</param>
        /// <param name="message">Event message text.</param>
        /// <param name="data">Additional event data.</param>
        public void Send(
            string id = null, 
            string severity = null, 
            string message = null, 
            object data = null) 
        {
            HttpInputEventInfo ei = 
                new HttpInputEventInfo(id, severity, message, data, metadata);
            // we use lock serializedEventsBatch to synchronize both 
            // serializedEventsBatch and serializedEvents
            lock (serializedEventsBatch)
            {
                eventsBatch.Add(ei);
                serializedEventsBatch.Append(SerializeEventInfo(ei));
                if (eventsBatch.Count >= batchSizeCount ||
                    serializedEventsBatch.Length >= batchSizeBytes)
                {
                    // there are enough events in the batch
                    Flush();
                }
            }
        }

        /// <summary>
        /// Flush all batched events immediately. 
        /// </summary>
        public void Flush()
        {
            lock (serializedEventsBatch)
            {
                if (serializedEventsBatch.Length > 0)
                {
                    PostEvents(eventsBatch, serializedEventsBatch.ToString());
                    serializedEventsBatch.Clear();
                    // we explicitly create a new events list instead to clear
                    // and reuse the old one because Flush works in async mode
                    // and can use use "previous" containers for error handling
                    eventsBatch = new List<HttpInputEventInfo>();                    
                }
            }
        }

        private async void PostEvents(
            List<HttpInputEventInfo> events, 
            String serializedEvents)
        {
            // encode data
            HttpContent content = new StringContent(
                serializedEvents, Encoding.UTF8, "application/json");
            try
            {
                // post data
                await httpClient.PostAsync(httpInputEndpointUri, content);
            }
            catch (HttpInputException e) 
            {
                e.Events = events;
                OnError(this, e);
            }
        }

        private void OnTimer(object state)
        {
            Flush();
        }

        private string SerializeEventInfo(HttpInputEventInfo eventInfo) 
        {
            return JsonConvert.SerializeObject(eventInfo);
        }

        #region IDispose

        private bool disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;
            if (disposing)
            {
                httpClient.Dispose();
            }
            disposed = true;
        }

        ~HttpInputSender()
        {
            Dispose(false);
        }

        #endregion
    }
}