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
        private StringBuilder serializedEventsBatch = new StringBuilder();

        /// <summary>
        /// HttpInputSender c-or.
        /// </summary>
        /// <param name="uri">Splunk server uri, for example https://localhost:8089.</param>
        /// <param name="token">Http input authorization token.</param>
        /// <param name="metadata">Logger metadata.</param>
        /// <param name="batchInterval">Batch interval in milliseconds.</param>
        /// <param name="batchSizeBytes">Batch max size.</param>
        /// <param name="batchSizeCount">MNax number of individual events in batch.</param>
        /// <param name="retriesOnError">Number of retries in case of connectivity problem.</param>
        public HttpInputSender(
            string uri, string token, Dictionary<string, string> metadata,
            uint batchInterval, uint batchSizeBytes, uint batchSizeCount, 
            uint retriesOnError)
        {
            this.url = uri + HttpInputPath;
            this.token = token;
            this.batchInterval = batchInterval;
            this.batchSizeBytes = batchSizeBytes;
            this.batchSizeCount = batchSizeCount;
            this.retriesOnError = retriesOnError;
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
            lock (serializedEventsBatch)
            {
                eventsBatch.Add(ei);
                serializedEventsBatch.Append(serializeEventInfo(ei));
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
                    postEventsAsync(eventsBatch, serializedEventsBatch.ToString());
                    serializedEventsBatch.Clear();
                    // we explicitly create a new events list instead to clear
                    // and reuse the old one because Flush works in async mode
                    // and can use use "previous" containers for error handling
                    eventsBatch = new List<HttpInputEventInfo>();                    
                }
            }
        }

        private async void postEventsAsync(
            List<HttpInputEventInfo> events, String serializedEvents)
        {
            // init http client
            HttpClient httpClient = new HttpClient();
            HttpContent content = new StringContent(
                serializedEvents, Encoding.UTF8, "application/json");
            // setup http input authentication 
            httpClient.DefaultRequestHeaders.Add(AuthorizationHeaderTag,
                string.Format(AuthorizationHeaderScheme, token));
            // send the data to http input
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