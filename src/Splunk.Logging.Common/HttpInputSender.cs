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

using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Splunk.Logging
{
    /// <summary>
    /// HTTP input client side implementation that collects, serializes and send 
    /// events to Splunk HTTP input endpoint. This class shouldn't be used directly
    /// by user applications.
    /// </summary>
    /// <remarks>
    /// * HttpInputSender is thread safe and Send(...) method may be called from
    /// different threads.
    /// * Events are are sending asynchronously and Send(...) method doesn't 
    /// block the caller code.
    /// * HttpInputSender has an ability to plug middleware components that act 
    /// before posting data.
    /// For example:
    /// <code>
    /// new HttpInputSender(uri: ..., token: ..., 
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
    /// the data to Splunk server. See HttpInputResendMiddleware.
    /// </remarks>
    public class HttpInputSender : HttpClientHandler
    {
        /// <summary>
        /// Post request delegate. 
        /// </summary>
        /// <param name="request">HTTP request.</param>
        /// <returns>Server HTTP response.</returns>
        public delegate Task<HttpResponseMessage> HttpInputHandler(
            HttpRequestMessage request);

        /// <summary>
        /// HTTP input middleware plugin.
        /// </summary>
        /// <param name="request">HTTP request.</param>
        /// <param name="next">A handler that posts data to the server.</param>
        /// <returns>Server HTTP response.</returns>
        public delegate Task<HttpResponseMessage> HttpInputMiddleware(
            HttpRequestMessage request, HttpInputHandler next);

        private const string HttpInputPath = "/services/receivers/token";
        private const string AuthorizationHeaderScheme = "Splunk";
        private Uri httpInputEndpointUri; // HTTP input endpoint full uri
        private HttpInputEventInfo.Metadata metadata; // logger metadata
        // events batching properties and collection 
        private int batchInterval = 0;
        private int batchSizeBytes = 0;
        private int batchSizeCount = 0;
        private List<HttpInputEventInfo> eventsBatch = new List<HttpInputEventInfo>();
        private StringBuilder serializedEventsBatch = new StringBuilder();
        private Timer timer;

        private HttpClient httpClient = null;
        private HttpInputMiddleware middleware = null;
        // counter for bookkeeping the async tasks 
        long activeAsyncTasksCount = 0;

        /// <summary>
        /// On error callbacks.
        /// </summary>
        public event EventHandler<HttpInputException> OnError = (s, e) => { };

        /// <param name="uri">Splunk server uri, for example https://localhost:8089.</param>
        /// <param name="token">HTTP input authorization token.</param>
        /// <param name="metadata">Logger metadata.</param>
        /// <param name="batchInterval">Batch interval in milliseconds.</param>
        /// <param name="batchSizeBytes">Batch max size.</param>
        /// <param name="batchSizeCount">MNax number of individual events in batch.</param>
        /// <param name="middleware">
        /// HTTP client middleware. This allows to plug an HttpClient handler that 
        /// intercepts logging HTTP traffic.
        /// </param>
        /// <remarks>
        /// Zero values for the batching params mean that batching is off. 
        /// </remarks>
        public HttpInputSender(
            Uri uri, string token, HttpInputEventInfo.Metadata metadata,
            int batchInterval, int batchSizeBytes, int batchSizeCount,
            HttpInputMiddleware middleware)
        {
            this.httpInputEndpointUri = new Uri(uri, HttpInputPath);
            this.batchInterval = batchInterval;
            this.batchSizeBytes = batchSizeBytes;
            this.batchSizeCount = batchSizeCount;
            this.metadata = metadata;
            this.middleware = middleware;

            // when size configuration setting is missing it's treated as "infinity",
            // i.e., any value is accepted.
            if (this.batchSizeCount == 0 && this.batchSizeBytes > 0)
            {
                this.batchSizeCount = int.MaxValue;
            }
            else if (this.batchSizeBytes == 0 && this.batchSizeCount > 0)
            {
                this.batchSizeBytes = int.MaxValue;
            }

            // setup the timer
            if (batchInterval != 0) // 0 means - no timer
            {
                timer = new Timer(OnTimer, null, batchInterval, batchInterval);
            }

            // setup HTTP client
            httpClient = middleware == null ? 
                new HttpClient() : 
                new HttpClient(this);
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(AuthorizationHeaderScheme, token);
        }

        /// <summary>
        /// Send an event to Splunk HTTP endpoint. Actual event send is done 
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
            string serializedEventInfo = SerializeEventInfo(ei);
            lock (serializedEventsBatch)
            {
                eventsBatch.Add(ei);
                serializedEventsBatch.Append(serializedEventInfo);
                if (eventsBatch.Count >= batchSizeCount ||
                    serializedEventsBatch.Length >= batchSizeBytes)
                {
                    // there are enough events in the batch
                    FlushUnlocked();
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
                FlushUnlocked();                
            }
        }

        /// <summary>
        /// Flush all events synchronously, i.e., flush and wait until all events
        /// are sent.
        /// </summary>
        public void FlushSync()
        {
            Flush();
            // wait until all pending tasks are done
            while(Interlocked.CompareExchange(ref activeAsyncTasksCount, 0, 0) != 0)
            {
                // wait for 100ms - not CPU intensive and doesn't delay process 
                // exit too much
                Thread.Sleep(100);
            }
        }

        /// <summary>
        /// Serialize event info into a json string
        /// </summary>
        /// <param name="eventInfo"></param>
        /// <returns></returns>
        public static string SerializeEventInfo(HttpInputEventInfo eventInfo)
        {
            return JsonConvert.SerializeObject(eventInfo);
        }

        private void FlushUnlocked()
        {
            if (serializedEventsBatch.Length > 0)
            {
                // post data and update tasks counter
                Interlocked.Increment(ref activeAsyncTasksCount);
                PostEvents(eventsBatch, serializedEventsBatch.ToString())
                    .ContinueWith((_) =>
                    {
                        Interlocked.Decrement(ref activeAsyncTasksCount);
                    });
                // we explicitly create new objects instead to clear and reuse 
                // the old ones because Flush works in async mode
                // and can use use "previous" containers
                serializedEventsBatch = new StringBuilder();
                eventsBatch = new List<HttpInputEventInfo>();
            }
    
        }

        private async Task<HttpStatusCode> PostEvents(
            List<HttpInputEventInfo> events,
            String serializedEvents)
        {
            // encode data
            HttpResponseMessage response = null;
            string serverReply = null;
            HttpStatusCode responseCode = HttpStatusCode.OK;
            HttpContent content = new StringContent(
                serializedEvents, Encoding.UTF8, "application/json");
            try
            {
                // post data
                response = await httpClient.PostAsync(httpInputEndpointUri, content);
                responseCode = response.StatusCode;
                if (responseCode != HttpStatusCode.OK && response.Content != null)
                {
                    // record server reply
                    serverReply = await response.Content.ReadAsStringAsync();
                }
            }
            catch (HttpInputException e)
            {
                e.Events = events;
                OnError(this, e);
            }
            catch (Exception e)
            {                
                OnError(this, new HttpInputException(
                    code: responseCode,
                    webException: e,
                    reply: serverReply,
                    response: response,
                    events: events
                ));
            }
            return responseCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
             // plug middleware into HTTP call
            return middleware(request, async (HttpRequestMessage) =>
            {                
                return await base.SendAsync(request, cancellationToken);                
            });
        }

        private void OnTimer(object state)
        {
            Flush();
        }

        #region HttpClientHandler.IDispose

        private bool disposed = false;

        override protected void Dispose(bool disposing)
        {
            if (disposed)
                return;
            if (disposing)
            {               
                httpClient.Dispose();
            }
            disposed = true;
            base.Dispose(disposing);
        }

        ~HttpInputSender()
        {
            Dispose(false);
        }

        #endregion
    }
}