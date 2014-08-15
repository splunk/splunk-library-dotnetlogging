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
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Splunk.Logging;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;

namespace Splunk.Logging
{
    public class TestEventFormatter : IEventTextFormatter
    {
        public void WriteEvent(EventEntry eventEntry, System.IO.TextWriter writer)
        {
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

    public class TestUdpEventSink
    {
        [Fact]
        public void TestUdpEventSinkWrites()
        {
            var socket = new MockSocket();

            var listener = new ObservableEventListener();
            listener.Subscribe(new UdpEventSink(socket, new TestEventFormatter()));
            
            var source = TestEventSource.GetInstance();
            listener.EnableEvents(source, EventLevel.LogAlways, Keywords.All);
            
            source.Message("Boris", "Meep");
            var result = socket.GetReceivedText();
            listener.Dispose();
        
            Assert.Equal("EventId=1 EventName=MessageInfo Level=Error " +
                "\"FormattedMessage=Meep - Boris\" \"message=Boris\" \"caller=Meep\"\r\n",
                result);

        }
    }
}