using Splunk.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Xunit;

namespace Splunk.Logging
{
    public class TestTcpTraceListener
    {
        [Fact]
        public void TraceToOpenTcpSocketWorks()
        {
            var mock = new MockSocketFactory();
            mock.AcceptingConnections = true;

            var writer = new TcpSocketWriter(
                IPAddress.Loopback,
                0,
                new ExponentialBackoffTcpConnectionPolicy(),
                10000,
                mock.TryOpenSocket);            

            var traceSource = new TraceSource("UnitTestLogger");
            traceSource.Listeners.Remove("Default");
            traceSource.Switch.Level = SourceLevels.All;
            traceSource.Listeners.Add(new TcpTraceListener(writer));   
            traceSource.TraceEvent(TraceEventType.Information, 100, "Boris");
            traceSource.Close();

            var result = mock.socket.GetReceivedText();
            var firstSpace = result.IndexOf(" ");

            var expected = result.Substring(0, firstSpace+1) + "UnitTestLogger Information: 100 : Boris\r\n";

            Assert.Equal(expected, result);
        }
    }
}
