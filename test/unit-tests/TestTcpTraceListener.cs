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
            string result = "";
            int port = 10000;
            var tcpListener = new TcpListener(IPAddress.Loopback, port);

            var receiver = new Thread(() => 
            {
                tcpListener.Start();   
                var client = tcpListener.AcceptTcpClient();
                var stream = client.GetStream();
                var reader = new StreamReader(stream);
                result = reader.ReadLine();
                client.Close();
            });
            receiver.Start();

            var sender = new Thread(new ThreadStart(() =>
            {
                var traceSource = new TraceSource("UnitTestLogger");
                traceSource.Listeners.Remove("Default");
                traceSource.Switch.Level = SourceLevels.All;
                var progress = new AwaitableProgress<EventWrittenProgressReport>();
                traceSource.Listeners.Add(new TcpTraceListener(IPAddress.Loopback, port, progress: progress));
                
                traceSource.TraceEvent(TraceEventType.Information, 100, "Boris");
                progress.AwaitProgressAsync().Wait();
                traceSource.Close();
            }));
            sender.Start();

            receiver.Join();
            sender.Join();

            var expected = "UnitTestLogger Information: 100 : Boris";
            var found = result.Substring(result.Length - expected.Length, expected.Length);
            Assert.Equal(expected, found);
        }
    }
}
