/*
 * Copyright 2015 Splunk, Inc.
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
using System.IO;
using System.Runtime.Serialization.Json;
using System.Net.Http;
using System.Text;
using System.Net;
using Newtonsoft.Json;

namespace Splunk.Logging
{
    public class HttpInputSender
    {
        private const string HttpInputPath = "/services/receivers/token";
        private const string AuthorizationHeaderTag = "Authorization";
        private const string AuthorizationHeaderScheme = "Splunk {0}";

        private string url;
        private string token;
        private List<HttpInputEventInfo> eventsBatch = new List<HttpInputEventInfo>();
        private Dictionary<string, string> metadata;

        public HttpInputSender(
            string uri, string token,
            uint batchInterval, uint batchSizeBytes, uint batchSizeCount, 
            uint retriesOnError,
            Dictionary<string, string> metadata)
        {
            this.url = uri + HttpInputPath;
            this.token = token;
            this.metadata = metadata;
        }

        public void Send(string id, string severity, string message) 
        {
            HttpInputEventInfo ei = new HttpInputEventInfo(id, severity, message, metadata);
            lock (eventsBatch)
            {
                eventsBatch.Add(ei);
                // todo - batching
                flush();
            }
        }

        public void flush()
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

        int before = 0, after = 0;

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

            before++;
            var response = await httpClient.PostAsync(url, content);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                after++;
                Console.WriteLine("{0} -- {1}", before, after);
            }
            if (response.StatusCode != HttpStatusCode.OK)
            {
                after++;
                Console.WriteLine("ERROR {0}", response.StatusCode);  
                // \todo - error handling
                // \todo - resend
            }
        }

        private string serializeEventInfo(HttpInputEventInfo eventInfo) 
        {
            return JsonConvert.SerializeObject(eventInfo);
        }
    }
}