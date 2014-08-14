using Splunk.Logging;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Xunit;

namespace Splunk.Logging
{
    public class UdpTestTraceListener : UdpTraceListener
    {
        public UdpTestTraceListener(ISocket socket) : base(socket) { }

        protected override string GetTimestamp()
        {
            return "[timestamp]";
        }
    }

    public class TestUdpTraceListener
    {
        [Fact]
        public void TraceToOpenUdpSocketWorks()
        {
            var socket = new MockSocket() { SocketFailed = false };

            var traceSource = new TraceSource("UnitTestLogger");
            traceSource.Listeners.Remove("Default");
            traceSource.Switch.Level = SourceLevels.All;
            traceSource.Listeners.Add(new UdpTestTraceListener(socket));

            traceSource.TraceEvent(TraceEventType.Information, 100, "Boris");
            var result = socket.GetReceivedText();
            traceSource.Close();

            Assert.Equal("[timestamp] UnitTestLogger Information: 100 : Boris\r\n", result);
        }
    }
}