using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Xunit;

namespace Splunk.Logging
{
    public class TestTcp
    {
        [Trait("integration-tests", "Splunk.Logging.TcpSocketWriter")]
        [Fact]
        public async Task TestTcpSocketWriter()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint) listener.Server.LocalEndPoint).Port;


            var writer = new TcpSocketWriter(IPAddress.Loopback, port, new ExponentialBackoffTcpReconnectionPolicy(),
                10);

            var listenerClient = await listener.AcceptTcpClientAsync();

            writer.Enqueue("This is a test.\r\n");

            var receiverReader = new StreamReader(listenerClient.GetStream());
            var line = await receiverReader.ReadLineAsync();

            Assert.Equal(line, "This is a test.");

            listenerClient.Close();
            listener.Stop();
        }

        [Trait("integration-tests", "Splunk.Logging.TcpTraceListener")]
        [Fact]
        public async Task TestTcpTraceListener()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint) listener.Server.LocalEndPoint).Port;


            var traceSource = new TraceSource("UnitTestLogger");
            traceSource.Listeners.Remove("Default");
            traceSource.Switch.Level = SourceLevels.All;
            traceSource.Listeners.Add(new TcpTraceListener(
                IPAddress.Loopback, port,
                new ExponentialBackoffTcpReconnectionPolicy()));

            var listenerClient = await listener.AcceptTcpClientAsync();

            traceSource.TraceEvent(TraceEventType.Information, 100, "Boris");

            var receiverReader = new StreamReader(listenerClient.GetStream());
            var line = await receiverReader.ReadLineAsync();

            Assert.True(line.EndsWith("UnitTestLogger Information: 100 : Boris"));

            listenerClient.Close();
            listener.Stop();
            traceSource.Close();
        }

        [Trait("integration-tests", "Splunk.Logging.TcpEventSink")]
        [Fact]
        public async Task TestEventSink()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.Server.LocalEndPoint).Port;

            var slabListener = new ObservableEventListener();
            slabListener.Subscribe(new TcpEventSink(IPAddress.Loopback, port,
                new ExponentialBackoffTcpReconnectionPolicy(),
                new TestEventFormatter()));
            var source = TestEventSource.GetInstance();
            slabListener.EnableEvents(source, EventLevel.LogAlways, Keywords.All);

            var listenerClient = await listener.AcceptTcpClientAsync();

            source.Message("Boris", "Meep");

            var receiverReader = new StreamReader(listenerClient.GetStream());
            var line = await receiverReader.ReadLineAsync();

            Assert.Equal(
                "EventId=1 EventName=MessageInfo Level=Error \"FormattedMessage=Meep - Boris\" \"message=Boris\" \"caller=Meep\"",
                line);

            listenerClient.Close();
            listener.Stop();
            slabListener.Dispose();
        }
    }
}
