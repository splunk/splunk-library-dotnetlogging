using System.Net.Sockets;
using System.Net;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Splunk.Logging;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UdpTraceListenerUnitTests
{
    [TestClass]
    public class TestUdpTraceListener
    {
        [TestMethod]
        public void TraceToOpenUdpSocketWorks()
        {
            StringBuilder sb = new StringBuilder();
            var receivingUdpClient = new UdpClient(0);
            int port = ((IPEndPoint)receivingUdpClient.Client.LocalEndPoint).Port;

            var receiver = new Thread((object r) =>
            {
                IPEndPoint endpoint = new IPEndPoint(IPAddress.Loopback, port);
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
            });
            sender.Start();

            receiver.Join();
            sender.Join();

            var received = sb.ToString();
            Assert.IsTrue(sb.ToString().EndsWith("UnitTestLogger Information: 100 : Boris\r\n"));
        }
    }
}