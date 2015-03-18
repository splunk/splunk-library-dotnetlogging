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
using System.Diagnostics;

namespace Splunk.Logging
{
    public class HttpInputTraceListener : TraceListener
    {
        private HttpInputSender sender;

        public HttpInputTraceListener()
            : base()
        {
            var meta = new Dictionary<string, string>();
            meta["index"] = "main";
            meta["source"] = "host";
            meta["sourcetype"] = "log";
            sender = new HttpInputSender("http://oizmerly-mbp:8089", "E6099437-3E1F-4793-90AB-0E5D9438A918", 0, 0, 0, 0, meta);
        }

        public void AddLoggingFailureHandler(Action<Exception> handler)
        {
        }

        
        public override void Write(string message) 
        {
            sender.Send(null, null, message);
        }

        public override void WriteLine(string message) {
            sender.Send(null, null, message);
        }

        public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id, params object[] data)
        {
            sender.Send(id.ToString(), eventType.ToString(), null);
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id)
        {
            sender.Send(id.ToString(), eventType.ToString(), null);
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            sender.Send(id.ToString(), eventType.ToString(), message);
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
        {
            string message = args != null ? string.Format(format, args) : format;
            sender.Send(id.ToString(), eventType.ToString(), message);
        }

        public override void TraceTransfer(TraceEventCache eventCache, string source, int id, string message, Guid relatedActivityId)
        {
            sender.Send(id.ToString(), null, message);
        }
        
        public override void Close()
        {
        }
    }
}
