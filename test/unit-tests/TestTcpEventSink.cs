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
using System.IO;

namespace Splunk.Logging
{
    public class TestTcpEventSink
    {
        [Fact]
        public void TestTcpEventSinkWrites()
        {
            var socketFactory = new MockSocketFactory();
            socketFactory.AcceptingConnections = true;
            var writer = new TcpSocketWriter(null, -1, new ExponentialBackoffTcpConnectionPolicy(), 3, socketFactory.TryOpenSocket);
            var listener = new ObservableEventListener();

            listener.Subscribe(new TcpEventSink(writer, new TestEventFormatter()));

            var source = TestEventSource.GetInstance();
            listener.EnableEvents(source, EventLevel.LogAlways, Keywords.All);

            source.Message("Boris", "Meep");
            socketFactory.socket.WaitForNEvents(1, 100);

            var result = socketFactory.socket.GetReceivedText();
            listener.Dispose();

            var expected = "EventId=1 EventName=MessageInfo Level=Error \"FormattedMessage=Meep - Boris\" \"message=Boris\" \"caller=Meep\"\r\n";
            Assert.Equal(expected, result);
        }
    }
}
