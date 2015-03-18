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
using System.Runtime.Serialization;

namespace Splunk.Logging
{
    [DataContract]
    public struct HttpInputEventInfo
    {
        public const string MetadataTimeTag = "time";
        public const string MetadataIndexTag = "index";
        public const string MetadataSourceTag = "source";
        public const string MetadataSourceTypeTag = "sourcetype";

        private Dictionary<string, string> metadata;

        [DataContract]
        public struct EventInfo
        {
            [DataMember(Name = "id", EmitDefaultValue = false)]
            public readonly string Id;

            [DataMember(Name = "severity", EmitDefaultValue = false)]
            public readonly string Severity;

            [DataMember(Name = "message", EmitDefaultValue = false)]
            public readonly string Message;

            public EventInfo(string id, string severity, string message)
            {
                Id = id;
                Severity = severity;
                Message = message;
            }
        }

        [DataMember(Name = MetadataTimeTag, EmitDefaultValue = false)]
        public readonly string Timestamp;

        [DataMember(Name = MetadataIndexTag, EmitDefaultValue = false)]
        public string Index { 
            get { return GetMetadataValue(MetadataIndexTag); }
            private set { } 
        }

        [DataMember(Name = MetadataSourceTag, EmitDefaultValue = false)]
        public string Source { 
            get { return GetMetadataValue(MetadataSourceTag); }
            private set { } 
        }

        [DataMember(Name = MetadataSourceTypeTag, EmitDefaultValue = false)]
        public string SourceType { 
            get { return GetMetadataValue(MetadataSourceTypeTag); } 
            private set { } 
        }

        [DataMember(Name = "event", EmitDefaultValue = false)]
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
