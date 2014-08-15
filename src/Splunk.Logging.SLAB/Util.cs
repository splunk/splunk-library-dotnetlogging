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
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using System;
using System.IO;

namespace Splunk.Logging
{
    /// <summary>
    /// An IEventTextFormatter for SLAB that writes in the form
    /// <code>{timestamp} EventId={...} EventName={...} Level={...} "FormattedMessage={...}"</code>
    /// </summary>
    public class SimpleEventTextFormatter : IEventTextFormatter
    {
        void IEventTextFormatter.WriteEvent(EventEntry eventEntry, TextWriter writer)
        {
            writer.Write(eventEntry.GetFormattedTimestamp("o") + " ");
            writer.Write("EventId=" + eventEntry.EventId + " ");
            writer.Write("EventName=" + eventEntry.Schema.EventName + " ");
            writer.Write("Level=" + eventEntry.Schema.Level + " ");
            writer.Write("\"FormattedMessage=" + eventEntry.FormattedMessage + "\" ");
            for (int i = 0; i < eventEntry.Payload.Count; i++)
            {
                try
                {
                    if (i != 0) writer.Write(" ");
                    writer.Write("\"{0}={1}\"", eventEntry.Schema.Payload[i], eventEntry.Payload[i]);
                }
                catch (Exception) { }
            }
            writer.WriteLine();
        }
    }
}
