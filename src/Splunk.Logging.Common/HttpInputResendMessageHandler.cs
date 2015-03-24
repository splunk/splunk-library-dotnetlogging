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

namespace Splunk.Logging
{
    /// <summary>
    /// Default http input message handler that tries to resend data in case of 
    /// a network problem.
    /// Usage:
    /// <code>
    /// trace.listeners.Add(new HttpInputTraceListener(
    ///     uri: "https://localhost:8089", 
    ///     token: "E6099437-3E1F-4793-90AB-0E5D9438A918",
    ///     new HttpInputResendMessageHandler(100) // retry up to 10 times
    /// );
    /// </code>
    /// </summary>
    public class HttpInputResendMessageHandler : HttpClientHandler
    {
        // List of http input server application error statuses. These statuses 
        // indicate non-transient problems that cannot be fixed by resending the 
        // data.
        private static readonly HttpStatusCode[] HttpInputApplicationErrors = 
        {
            HttpStatusCode.Forbidden,
            HttpStatusCode.MethodNotAllowed,
            HttpStatusCode.BadRequest                  
        };

        private uint retriesOnError = 0;
            
        public HttpInputResendMessageHandler(uint retriesOnError)
        {
            this.retriesOnError = retriesOnError;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = null;
            HttpStatusCode statusCode = HttpStatusCode.OK;
            WebException webException = null;
            string serverReply = null;
            // retry sending data until success
            for (uint retriesCount = 0; retriesCount <= retriesOnError; retriesCount++)
            {
                try
                {
                    response = await base.SendAsync(request, cancellationToken);
                    statusCode = response.StatusCode;
                    if (statusCode == HttpStatusCode.OK)
                    {
                        // the data has been sent successfully
                        webException = null;
                        break;
                    }
                    else if (Array.IndexOf(HttpInputApplicationErrors, statusCode) >= 0)
                    {
                        // Http input application error detected - resend wouldn't help
                        // in this case. Record server reply and break.
                        serverReply = await response.Content.ReadAsStringAsync();
                        break;
                    }
                    else
                    {
                        // retry
                    }
                }
                catch (System.Net.WebException e)
                {
                    // connectivity problem - record exception and retry
                    webException = e;
                }
            }
            if (statusCode != HttpStatusCode.OK || webException != null)
            {
                throw new HttpInputException(
                    code: statusCode,
                    webException: webException,
                    reply: serverReply,
                    response: response,
                    events: null
                );
            }
            return response;
        }
    }
}