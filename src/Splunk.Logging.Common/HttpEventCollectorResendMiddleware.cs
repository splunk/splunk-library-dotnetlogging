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
using System.Net.Http;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Splunk.Logging
{
    /// <summary>
    /// HTTP event collector middleware plug in that implements a simple resend policy. 
    /// When HTTP post reply isn't an application error we try to resend the data.
    /// Usage:
    /// <code>
    /// trace.listeners.Add(new HttpEventCollectorTraceListener(
    ///     uri: new Uri("https://localhost:8088"), 
    ///     token: "E6099437-3E1F-4793-90AB-0E5D9438A918",
    ///     new HttpEventCollectorResendMiddleware(10).Plugin // retry 10 times
    /// );
    /// </code>
    /// </summary>
    public class HttpEventCollectorResendMiddleware
    {
        // List of HTTP event collector server application error statuses. These statuses 
        // indicate non-transient problems that cannot be fixed by resending the 
        // data.
        private static readonly HttpStatusCode[] HttpEventCollectorApplicationErrors = 
        {
            HttpStatusCode.Forbidden,
            HttpStatusCode.MethodNotAllowed,
            HttpStatusCode.BadRequest                  
        };

        private const int RetryDelayCeiling = 60 * 1000; // 1 minute     
        private int retriesOnError = 0;

        /// <param name="retriesOnError">
        /// Max number of retries before reporting an error.
        /// </param>
        public HttpEventCollectorResendMiddleware(int retriesOnError)
        {
            this.retriesOnError = retriesOnError;
        }

        /// <summary>
        /// Callback that should be used as middleware in HttpEventCollectorSender
        /// </summary>
        /// <param name="request"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> Plugin(
            string token, 
            List<HttpEventCollectorEventInfo> events, 
            HttpEventCollectorSender.HttpEventCollectorHandler next)
        {          
            HttpResponseMessage response = null;
            HttpStatusCode statusCode = HttpStatusCode.OK;
            Exception webException = null;
            string serverReply = null;
            int retryDelay = 1000; // start with 1 second
            // retry sending data until success
            for (int retriesCount = 0; retriesCount <= retriesOnError; retriesCount++)
            {
                try
                {
                    response = await next(token, events);
                    statusCode = response.StatusCode;
                    if (statusCode == HttpStatusCode.OK)
                    {
                        // the data has been sent successfully
                        webException = null;
                        break;
                    }
                    else if (Array.IndexOf(HttpEventCollectorApplicationErrors, statusCode) >= 0)
                    {
                        // HTTP event collector application error detected - resend wouldn't help
                        // in this case. Record server reply and break.
                        if (response.Content != null)
                        {
                            serverReply = await response.Content.ReadAsStringAsync();
                        }
                        break;
                    }
                    else
                    {
                        // retry
                    }
                }
                catch (Exception e)
                {
                    // connectivity problem - record exception and retry
                    webException = e;
                }
                // wait before next retry
                await Task.Delay(retryDelay);
                // increase delay exponentially
                retryDelay = Math.Min(RetryDelayCeiling, retryDelay * 2);
            }
            if (statusCode != HttpStatusCode.OK || webException != null)
            {
                throw new HttpEventCollectorException(
                    code: statusCode,
                    webException: webException,
                    reply: serverReply,
                    response: response
                );
            }
            return response;
        }
    }
}