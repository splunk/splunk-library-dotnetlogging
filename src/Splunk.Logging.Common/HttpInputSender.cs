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

namespace Splunk.Logging
{
    /// <summary>
    /// Http input client side implementation that collects, serializes and send 
    /// events to Splunk http input endpoint. This class shouldn't be used directly
    /// by user applications.
    /// </summary>
    /// <remarks>
    /// HttpInputSender is thread safe. Events are are sending asynchronously thus 
    /// call to the Send method doesn't block a user application.
    /// </remarks>
    public class HttpInputSender
    {
        private const string HttpInputPath = "/services/receivers/token";
        private const string AuthorizationHeaderTag = "Authorization";
        private const string AuthorizationHeaderScheme = "Splunk {0}";

        private string url; // http input endpoint full url
        private string token; // authorization token
        private Dictionary<string, string> metadata; // logger metadata

        // events batching properties and collection 
        uint batchInterval = 0; 
        uint batchSizeBytes = 0;
        uint batchSizeCount = 0;
        uint retriesOnError = 0;
        private List<HttpInputEventInfo> eventsBatch = new List<HttpInputEventInfo>();
        
        /// <summary>
        /// HttpInputSender c-or.
        /// </summary>
        /// <param name="uri">Splunk server uri, for example https://localhost:8089.</param>
        /// <param name="token">Http input authorization token.</param>
        /// <param name="batchInterval">Batch interval in milliseconds.</param>
        /// <param name="batchSizeBytes">Batch max size.</param>
        /// <param name="batchSizeCount">MNax number of individual events in batch.</param>
        /// <param name="retriesOnError">Number of retries in case of connectivity problem.</param>
        /// <param name="metadata">Logger metadata.</param>
        public HttpInputSender(
            string uri, string token,
            uint batchInterval, uint batchSizeBytes, uint batchSizeCount, 
            uint retriesOnError,
            Dictionary<string, string> metadata)
        {
            this.url = uri + HttpInputPath;
            this.token = token;
            this.batchInterval = batchInterval;
            this.batchSizeBytes = batchSizeBytes;
            this.batchSizeCount = batchSizeCount;
            this.retriesOnError = retriesOnError;
            this.metadata = metadata;
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
            lock (eventsBatch)
            {
                eventsBatch.Add(ei);
                // @TODO - batching
                Flush();
            }
        }

        /// <summary>
        /// Flush all batched events immediately. 
        /// </summary>
        public void Flush()
        {
            lock (eventsBatch)
            {
                if (eventsBatch.Count > 0)
                {
                    postEventsAsync(eventsBatch);
                    eventsBatch.Clear();
                }
            }
        }

        private async void postEventsAsync(List<HttpInputEventInfo> events)
        {
            // append all events into a single string
            StringBuilder sb = new StringBuilder();
            events.ForEach((e) => sb.Append(serializeEventInfo(e)));

            HttpClient httpClient = new HttpClient();
            HttpContent content = new StringContent(
                sb.ToString(), Encoding.UTF8, "application/json");

            httpClient.DefaultRequestHeaders.Add(AuthorizationHeaderTag,
                string.Format(AuthorizationHeaderScheme, token));

            try
            {
                var response = await httpClient.PostAsync(url, content);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    // @TODO - error handling
                    // @TODO - resend
                    Console.WriteLine("ERROR {0}", response.StatusCode);
                }

            }
            catch (System.Net.WebException)
            {
                // @TODO - error handling
            }    
        }

        private string serializeEventInfo(HttpInputEventInfo eventInfo) 
        {
            return JsonConvert.SerializeObject(eventInfo);
        }
    }
}