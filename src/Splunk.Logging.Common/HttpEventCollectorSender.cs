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
    /// HTTP event collector client side implementation that collects, serializes and send 
    /// events to Splunk HTTP event collector endpoint. This class shouldn't be used directly
    /// by user applications.
    /// </summary>
    /// <remarks>
    /// * HttpEventCollectorSender is thread safe and Send(...) method may be called from
    /// different threads.
    /// * Events are are sending asynchronously and Send(...) method doesn't 
    /// block the caller code.
    /// * HttpEventCollectorSender has an ability to plug middleware components that act 
    /// before posting data.
    /// For example:
    /// <code>
    /// new HttpEventCollectorSender(uri: ..., token: ..., 
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
    public class HttpEventCollectorSender : IDisposable
    {
        /// <summary>
        /// Post request delegate. 
        /// </summary>
        /// <param name="request">HTTP request.</param>
        /// <returns>Server HTTP response.</returns>
        public delegate Task<HttpResponseMessage> HttpEventCollectorHandler(
            string token, List<HttpEventCollectorEventInfo> events);

        /// <summary>
        /// HTTP event collector middleware plugin.
        /// </summary>
        /// <param name="request">HTTP request.</param>
        /// <param name="next">A handler that posts data to the server.</param>
        /// <returns>Server HTTP response.</returns>
        public delegate Task<HttpResponseMessage> HttpEventCollectorMiddleware(
            string token, List<HttpEventCollectorEventInfo> events, HttpEventCollectorHandler next);

        private const string HttpContentTypeMedia = "application/json";
        private const string HttpEventCollectorPath = "/services/collector/event/1.0";
        private const string AuthorizationHeaderScheme = "Splunk";
        private Uri httpEventCollectorEndpointUri; // HTTP event collector endpoint full uri
        private HttpEventCollectorEventInfo.Metadata metadata; // logger metadata
        private string token; // authorization token

        // events batching properties and collection 
        private int batchInterval = 0;
        private int batchSizeBytes = 0;
        private int batchSizeCount = 0;
        private List<HttpEventCollectorEventInfo> eventsBatch = new List<HttpEventCollectorEventInfo>();
        private StringBuilder serializedEventsBatch = new StringBuilder();
        private Timer timer;

        private HttpClient httpClient = null;
        private HttpEventCollectorMiddleware middleware = null;
        // counter for bookkeeping the async tasks 
        private long activeAsyncTasksCount = 0;

        /// <summary>
        /// On error callbacks.
        /// </summary>
        public event Action<HttpEventCollectorException> OnError = (e) => { };

        /// <param name="uri">Splunk server uri, for example https://localhost:8089.</param>
        /// <param name="token">HTTP event collector authorization token.</param>
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
        public HttpEventCollectorSender(
            Uri uri, string token, HttpEventCollectorEventInfo.Metadata metadata,
            int batchInterval, int batchSizeBytes, int batchSizeCount,
            HttpEventCollectorMiddleware middleware)
        {
            this.httpEventCollectorEndpointUri = new Uri(uri, HttpEventCollectorPath);
            this.batchInterval = batchInterval;
            this.batchSizeBytes = batchSizeBytes;
            this.batchSizeCount = batchSizeCount;
            this.metadata = metadata;
            this.token = token;
            this.middleware = middleware;

            // special case - if batch interval is specified without size and count
            // they are set to "infinity", i.e., batch may have any size 
            if (this.batchInterval > 0 && this.batchSizeBytes == 0 && this.batchSizeCount == 0)
            {
                this.batchSizeBytes = this.batchSizeCount = int.MaxValue;
            }

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
            httpClient = new HttpClient();
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
            HttpEventCollectorEventInfo ei =
                new HttpEventCollectorEventInfo(id, severity, message, data, metadata);
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
        /// Flush all event.
        /// </summary>
        public Task FlushAsync()
        {            
            return new Task(() => 
            {
                FlushSync();
            });
        }

        /// <summary>
        /// Serialize event info into a json string
        /// </summary>
        /// <param name="eventInfo"></param>
        /// <returns></returns>
        public static string SerializeEventInfo(HttpEventCollectorEventInfo eventInfo)
        {
            return JsonConvert.SerializeObject(eventInfo);
        }

        /// <summary>
        /// Flush all batched events immediately. 
        /// </summary>
        private void Flush()
        {
            lock (serializedEventsBatch)
            {
                FlushUnlocked();
            }
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
                eventsBatch = new List<HttpEventCollectorEventInfo>();
            }
    
        }

        private async Task<HttpStatusCode> PostEvents(
            List<HttpEventCollectorEventInfo> events,
            String serializedEvents)
        {
            // encode data
            HttpResponseMessage response = null;
            string serverReply = null;
            HttpStatusCode responseCode = HttpStatusCode.OK;
            try
            {
                // post data
                HttpEventCollectorHandler next = (t, e) =>
                {
                    HttpContent content = new StringContent(serializedEvents, Encoding.UTF8, HttpContentTypeMedia);
                    return httpClient.PostAsync(httpEventCollectorEndpointUri, content);
                };
                HttpEventCollectorHandler postEvents = (t, e) =>
                {
                    return middleware == null ?
                        next(t, e) : middleware(t, e, next);
                };
                response = await postEvents(token, events);
                responseCode = response.StatusCode;
                if (responseCode != HttpStatusCode.OK && response.Content != null)
                {
                    // record server reply
                    serverReply = await response.Content.ReadAsStringAsync();
                    OnError(new HttpEventCollectorException(
                        code: responseCode,
                        webException: null,
                        reply: serverReply,
                        response: response,
                        events: events
                    ));
                }
            }
            catch (HttpEventCollectorException e)
            {
                e.Events = events;
                OnError(e);
            }
            catch (Exception e)
            {                
                OnError(new HttpEventCollectorException(
                    code: responseCode,
                    webException: e,
                    reply: serverReply,
                    response: response,
                    events: events
                ));
            }
            return responseCode;
        }

        private void OnTimer(object state)
        {
            Flush();
        }

        #region HttpClientHandler.IDispose

        private bool disposed = false;

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;
            if (disposing)
            {
                if (timer != null)
                {
                    timer.Dispose();
                }
                httpClient.Dispose();
            }
            disposed = true;
        }

        ~HttpEventCollectorSender()
        {
            Dispose(false);
        }

        #endregion
    }
}