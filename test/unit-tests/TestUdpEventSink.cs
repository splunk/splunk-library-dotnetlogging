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

namespace Splunk.Logging
{
    public class TestUdpEventSink
    {
        [EventSource(Name="TestEventSource")]
        public class TestEventSource : EventSource
        {
            public class Keywords
            {
                public const EventKeywords Page = (EventKeywords)1;
                public const EventKeywords DataBase = (EventKeywords)2;
                public const EventKeywords Diagnostic = (EventKeywords)4;
                public const EventKeywords Perf = (EventKeywords)8;
            }

            public class Tasks
            {
                public const EventTask Page = (EventTask)1;
                public const EventTask DBQuery = (EventTask)2;
            }

            [Event(1, Message = "{1} - {0}", Level = EventLevel.Error)]
            internal void Message(string message, string caller)
            {
                this.WriteEvent(1, message, caller);
            }
        }

        [Fact]
        public void TestUdpEventSinkWrites()
        {
            var sb = new StringBuilder();
            var receivingUdpClient = new UdpClient(0);
            int port = ((IPEndPoint)receivingUdpClient.Client.LocalEndPoint).Port;

            var receiver = new Thread((object r) =>
            {
                var endpoint = new IPEndPoint(IPAddress.Loopback, port);
                var receivedBytes = receivingUdpClient.Receive(ref endpoint);
                ((StringBuilder)r).Append(Encoding.UTF8.GetString(receivedBytes));
            });
            receiver.Start(sb);

            var sender = new Thread(() =>
            {
                var listener = new ObservableEventListener();
                listener.Subscribe(new UdpEventSink(IPAddress.Loopback, port));
                var source = new TestEventSource();
                listener.EnableEvents(source, EventLevel.LogAlways, Keywords.All);
                source.Message("Boris", "Meep");

            });
            sender.Start();

            receiver.Join();
            sender.Join();

            var received = sb.ToString();

            string timestamp = received.Split(new char[] {' '})[0];
            string expected = timestamp + " EventId=1 EventName=MessageInfo Level=Error " +
                "\"FormattedMessage=Meep - Boris\" \"message=Boris\" \"caller=Meep\"";
            Assert.Equal(expected.Trim(), received.Trim());
        }
    }
}
