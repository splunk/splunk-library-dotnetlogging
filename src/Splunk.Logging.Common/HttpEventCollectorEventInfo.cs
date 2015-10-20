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

using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Splunk.Logging
{
    /// <summary>
    /// HttpEventCollectorEventInfo is a wrapper container for .NET events information.
    /// An instance of HttpEventCollectorEventInfo can be easily serialized into json
    /// format using JsonConvert.SerializeObject. 
    /// </summary>
    public class HttpEventCollectorEventInfo
    {
        #region metadata

        /// <summary>
        /// Common metadata tags that can be specified by HTTP event collector logger.
        /// </summary>
        public const string MetadataTimeTag       = "time";
        public const string MetadataIndexTag      = "index";
        public const string MetadataSourceTag     = "source";
        public const string MetadataSourceTypeTag = "sourcetype";
        public const string MetadataHostTag       = "host";
        
        /// <summary>
        /// Metadata container
        /// </summary>
        public class Metadata
        {
            public string Index { get; private set; }
            public string Source { get; private set; }
            public string SourceType { get; private set; }
            public string Host { get; private set; }

            public Metadata(
                string index = null, 
                string source = null, 
                string sourceType = null, 
                string host = null)                
            {
                this.Index = index;
                this.Source = source;
                this.SourceType = sourceType;
                this.Host = host;
            }
        }

        private Metadata metadata;

        #endregion

        /// <summary>
        /// A wrapper for logger event information.
        /// </summary>
        public struct LoggerEvent
        {
            /// <summary>
            /// Logging event id.
            /// </summary>
            [JsonProperty(PropertyName = "id", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string Id { get; private set; }

            /// <summary>
            /// Logging event severity info.
            /// </summary>
            [JsonProperty(PropertyName = "severity", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string Severity { get; private set; }

            /// <summary>
            /// Logging event message.
            /// </summary>
            [JsonProperty(PropertyName = "message", DefaultValueHandling = DefaultValueHandling.Ignore)]
           public string Message { get; private set; }

            /// <summary>
            /// Auxiliary event data.
            /// </summary>
            [JsonProperty(PropertyName = "data", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public object Data { get; private set; }

            /// <summary>
            /// LoggerEvent c-or.
            /// </summary>
            /// <param name="id">Event id.</param>
            /// <param name="severity">Event severity info.</param>
            /// <param name="message">Event message.</param>
            /// <param name="data">Event data.</param>
            internal LoggerEvent(string id, string severity, string message, object data) : this()
            {
                this.Id = id;
                this.Severity = severity;
                this.Message = message;
                this.Data = data;
            }
        }

        /// <summary>
        /// Event timestamp in epoch format.
        /// </summary>
        [JsonProperty(PropertyName = MetadataTimeTag)]
        public string Timestamp { get; private set; }

        /// <summary>
        /// Event metadata index.
        /// </summary>
        [JsonProperty(PropertyName = MetadataIndexTag, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Index { get { return metadata.Index; } }

        /// <summary>
        /// Event metadata source.
        /// </summary>
        [JsonProperty(PropertyName = MetadataSourceTag, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Source { get { return metadata.Source; } }

        /// <summary>
        /// Event metadata sourcetype.
        /// </summary>
        [JsonProperty(PropertyName = MetadataSourceTypeTag, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string SourceType { get { return metadata.SourceType; } }

        /// <summary>
        /// Event metadata host.
        /// </summary>
        [JsonProperty(PropertyName = MetadataHostTag, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Host { get { return metadata.Host; } }

        /// <summary>
        /// Logger event info.
        /// </summary>
        [JsonProperty(PropertyName = "event", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public LoggerEvent Event { get; private set; }

        /// <summary>
        /// HttpEventCollectorEventInfo c-or.
        /// </summary>
        /// <param name="id">Event id.</param>
        /// <param name="severity">Event severity info.</param>
        /// <param name="message">Event message text.</param>
        /// <param name="data">Event auxiliary data.</param>
        /// <param name="metadata">Logger metadata.</param>
        public HttpEventCollectorEventInfo(
            string id, string severity, string message, object data, Metadata metadata) 
            : this(DateTime.UtcNow, id, severity, message, data, metadata)
        {
        }

        /// <summary>
        /// HttpEventCollectorEventInfo c-or.
        /// </summary>
        /// <param name="datetime">Time value to use.</param>
        /// <param name="id">Event id.</param>
        /// <param name="severity">Event severity info.</param>
        /// <param name="message">Event message text.</param>
        /// <param name="data">Event auxiliary data.</param>
        /// <param name="metadata">Logger metadata.</param>
        public HttpEventCollectorEventInfo(
            DateTime datetime, string id, string severity, string message, object data, Metadata metadata)
        {
            double epochTime = (datetime - new DateTime(1970, 1, 1)).TotalSeconds;
            Timestamp = epochTime.ToString("#.000"); // truncate to 3 digits after floating point
            this.metadata = metadata ?? new Metadata();
            Event = new LoggerEvent(id, severity, message, data);
        }
    }
}
