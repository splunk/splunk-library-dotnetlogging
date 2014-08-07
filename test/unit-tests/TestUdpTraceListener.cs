using Splunk.Logging;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Xunit;

namespace Splunk.Logging
{
    public class TestUdpTraceListener
    {
        [Fact]
        public void TraceToOpenUdpSocketWorks()
        {
            var sb = new StringBuilder();
            int port = 11003;
            var receivingUdpClient = new UdpClient(port);

            var receiver = new Thread((object r) =>
            {
                var endpoint = new IPEndPoint(IPAddress.Loopback, port);
                var receivedBytes = receivingUdpClient.Receive(ref endpoint);
                ((StringBuilder)r).Append(Encoding.UTF8.GetString(receivedBytes));
            });
            receiver.Start(sb);

            var sender = new Thread(() =>
            {
                var traceSource = new TraceSource("UnitTestLogger");
                traceSource.Listeners.Remove("Default");
                traceSource.Switch.Level = SourceLevels.All;
                traceSource.Listeners.Add(new UdpTraceListener(IPAddress.Loopback, port));
                traceSource.TraceEvent(TraceEventType.Information, 100, "Boris");
                traceSource.Close();
            });
            sender.Start();

            receiver.Join();
            sender.Join();

            var received = sb.ToString();

            var expected = "UnitTestLogger Information: 100 : Boris\r\n";
            var found = sb.ToString();
            var foundTail = found.Substring(found.Length-expected.Length, expected.Length);
            Assert.Equal(expected, foundTail);
        }
    }
}