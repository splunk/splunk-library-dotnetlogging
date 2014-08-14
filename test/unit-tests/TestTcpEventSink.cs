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
            throw new NotImplementedException();
            string result = "";
            int port = 11000;
            var tcpListener = new TcpListener(IPAddress.Loopback, port);

            
            /*
                var listener = new ObservableEventListener();
                var progress = new AwaitableProgress<EventWrittenProgressReport>();
                listener.Subscribe(new TcpEventSink(IPAddress.Loopback, port, progress: progress));
                var source = TestEventSource.GetInstance();
                listener.EnableEvents(source, EventLevel.LogAlways, Keywords.All);
                source.Message("Boris", "Meep");
                progress.AwaitProgressAsync().Wait();
                listener.Dispose();
             * */
            var expected = "EventId=1 EventName=MessageInfo Level=Error \"FormattedMessage=Meep - Boris\" \"message=Boris\" \"caller=Meep\"";
            var found = result.Substring(result.Length - 1 - expected.Length, expected.Length);
            Assert.Equal(expected, found);
        }
    }
}
