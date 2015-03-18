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

using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Splunk.Logging
{
    [JsonObject(MemberSerialization.OptIn)]
    public struct HttpInputEventInfo
    {
        public const string MetadataTimeTag = "time";
        public const string MetadataIndexTag = "index";
        public const string MetadataSourceTag = "source";
        public const string MetadataSourceTypeTag = "sourcetype";

        private Dictionary<string, string> metadata;

        public struct EventInfo
        {
            [JsonProperty(PropertyName = "id", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public readonly string Id;

           [JsonProperty(PropertyName = "severity", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public readonly string Severity;

            [JsonProperty(PropertyName = "message", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public readonly string Message;

            public EventInfo(string id, string severity, string message)
            {
                Id = id;
                Severity = severity;
                Message = message;
            }
        }

        [JsonProperty(PropertyName = "time")]
        public readonly string Timestamp;

        [JsonProperty(PropertyName = "index", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Index { 
            get { return GetMetadataValue(MetadataIndexTag); }
            private set { } 
        }

        [JsonProperty(PropertyName = "source", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Source { 
            get { return GetMetadataValue(MetadataSourceTag); }
            private set { } 
        }

        [JsonProperty(PropertyName = "sourcetype", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string SourceType { 
            get { return GetMetadataValue(MetadataSourceTypeTag); } 
            private set { } 
        }

        [JsonProperty(PropertyName = "event", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public readonly EventInfo Event;

        public HttpInputEventInfo(string id, string severity, string message, Dictionary<string, string> metadata)
        {
            this.metadata = metadata; 
            Event = new EventInfo(id, severity, message);
            Timestamp = ((ulong)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds).ToString();
        }

        public int Size()
        {
            return Event.Severity.Length + Event.Message.Length;
        }

        private string GetMetadataValue(string tag)
        {
            string value = null;
            metadata.TryGetValue(tag, out value);
            return value;
        }
    }
}
