/*
 * Copyright 2014 Splunk, Inc.
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
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Splunk.Logging
{
    [EventSource(Name = "TestEventSource")]
    public class TestEventSource : EventSource
    {
        private static TestEventSource instance = null;

        public class Keywords
        {
        }

        public class Tasks
        {
        }

        [Event(1, Message = "{1} - {0}", Level = EventLevel.Error)]
        internal void Message(string message, string caller)
        {
            this.WriteEvent(1, message, caller);
        }

        public static TestEventSource GetInstance()
        {
            if (instance == null)
            {
                instance = new TestEventSource();
            }
            return instance;
        }
    }
}
