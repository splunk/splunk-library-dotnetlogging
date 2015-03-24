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
using System.Net;
using System.Net.Http;

namespace Splunk.Logging
{
    /// <summary>
    /// Http input exception. This class is used when an http input client is 
    /// unable to send events to the server;
    /// </summary>
    public class HttpInputException : Exception 
    {
        /// <summary>
        /// Http status code.
        /// </summary>
        public HttpStatusCode StatusCode { get; private set; }

        /// <summary>
        /// Exception thrown by http client when sending the data. This value 
        /// can be null.
        /// </summary>
        public Exception WebException { get; private set; }
        
        /// <summary>
        /// Reply from the Splunk server. This value can be null.
        /// </summary>
        public string ServerReply { get; private set; }

        /// <summary>
        /// Reply from the Splunk server. This value can be null.
        /// </summary>
        public HttpResponseMessage Response { get; private set; }

        /// <summary>
        /// List of events that caused the problem.
        /// </summary>
        public List<HttpInputEventInfo> Events { get; set; }

        /// <summary>
        /// Http input exception container.
        /// </summary>
        /// <param name="code">Http status code.</param>
        /// <param name="webException">Exception thrown by http client when sending the data.</param>
        /// <param name="reply">Splunk server reply.</param>
        /// <param name="events">List of events that caused the problem.</param>
        public HttpInputException(
            HttpStatusCode code, 
            Exception webException, 
            string reply, 
            HttpResponseMessage response,
            List<HttpInputEventInfo> events)
        {
            StatusCode = code;
            WebException = webException;
            ServerReply = reply;
            Response = response;
            Events = events;
        }
    }
}