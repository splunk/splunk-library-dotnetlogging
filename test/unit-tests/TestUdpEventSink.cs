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