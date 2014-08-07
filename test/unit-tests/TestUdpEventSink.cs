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
        [Fact]
        public void TestUdpEventSinkWrites()
        {
            var sb = new StringBuilder();
            int port = 11002;
            var receivingUdpClient = new UdpClient(port);

            var receiver = new Thread((object r) =>
            {
                var endpoint = new IPEndPoint(IPAddress.Loopback, port);
                var receivedBytes = receivingUdpClient.Receive(ref endpoint);
                ((StringBuilder)r).Append(Encoding.UTF8.GetString(receivedBytes));
            });
            receiver.Start(sb);

            ThreadStart f = () =>
            {
                var listener = new ObservableEventListener();
                listener.Subscribe(new UdpEventSink(IPAddress.Loopback, port));
                var source = TestEventSource.GetInstance();
                listener.EnableEvents(source, EventLevel.LogAlways, Keywords.All);
                source.Message("Boris", "Meep");
                listener.Dispose();
            };
            var sender = new Thread(f);
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