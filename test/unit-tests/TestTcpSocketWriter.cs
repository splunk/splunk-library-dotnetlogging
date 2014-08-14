using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Splunk.Logging
{
    public class TestTcpSocketWriter
    {
        class TryOnceTcpConnectionPolicy : TcpConnectionPolicy
        {
            public ISocket Connect(Func<System.Net.IPAddress, int, ISocket> connect, System.Net.IPAddress host, int port, System.Threading.CancellationToken cancellationToken)
            {
                try
                {
                    return connect(host, port);
                }
                catch (SocketException e)
                {
                    throw new TcpReconnectFailure("Reconnect failed: " + e.Message);
                }
            }
        }

        [Fact]
        public void TestReconnectFailure()
        {
            var socketFactory = new MockSocketFactory();
            string failedMessage = null;

            socketFactory.AcceptingConnections = true;
            var writer = new TcpSocketWriter(null, -1, new TryOnceTcpConnectionPolicy(),
                2, socketFactory.TryOpenSocket);

            writer.LoggingFailureHandler += (ex) => { failedMessage = ex.Message; };
            socketFactory.AcceptingConnections = false;
            socketFactory.socket.SocketFailed = true;
            writer.Enqueue("test");

            writer.Dispose();
            Assert.Equal(
                "Reconnect failed: No connection could be made because the target machine actively refused it",
                failedMessage);
        }

        class TriggerableTcpConnectionPolicy : TcpConnectionPolicy
        {
            private AutoResetEvent trigger = new AutoResetEvent(false);

            public ISocket Connect(Func<System.Net.IPAddress, int, ISocket> connect, System.Net.IPAddress host, int port, System.Threading.CancellationToken cancellationToken)
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    trigger.WaitOne();
                    return connect(host, port);
                }
                return null;
            }

            public void TriggerConnect()
            {
                trigger.Set();
            }
        }

        [Fact]
        public void TestEventsQueuedWhileWaitingForInitialConnection()
        {
            var socketFactory = new MockSocketFactory();
            socketFactory.AcceptingConnections = true;

            var policy = new TriggerableTcpConnectionPolicy();
            var writer = new TcpSocketWriter(null, -1, policy,
                2, socketFactory.TryOpenSocket);

            writer.Enqueue("Event 1\r\n");
            writer.Enqueue("Event 2\r\n");

            Assert.Equal(null, socketFactory.socket);

            policy.TriggerConnect();
            Assert.Equal("Event 1\r\nEvent 2\r\n", socketFactory.socket.GetReceivedText());
        }

        [Fact]
        public void TestEventsQueuedDuringDisconnectAreSentLater()
        {
            var socketFactory = new MockSocketFactory();
            socketFactory.AcceptingConnections = true;

            var policy = new TriggerableTcpConnectionPolicy();
            var progress = new AwaitableProgress<TcpSocketWriter.ProgressReport>();
            var writer = new TcpSocketWriter(null, -1, policy,
                5, socketFactory.TryOpenSocket)
                {
                    Progress = progress
                };

            var p = progress.AwaitProgressAsync();
            policy.TriggerConnect();

            // Make sure we are connected
            writer.Enqueue("Event 0\r\n");
            progress.AwaitProgressAsync().Wait();
            Assert.Equal("Event 0\r\n", socketFactory.socket.GetReceivedText());
            
            socketFactory.socket.SocketFailed = true;
            writer.Enqueue("Event 1\r\n");
            writer.Enqueue("Event 2\r\n");
            Assert.Equal("Event 0\r\n", socketFactory.socket.GetReceivedText());

            policy.TriggerConnect();
            progress.AwaitProgressAsync().Wait();
            Assert.Equal("Event 1\r\nEvent 2\r\n", socketFactory.socket.GetReceivedText());
        }

        [Fact]
        public void TestEventsQueuedCanBeDropped()
        {
            var socketFactory = new MockSocketFactory();
            socketFactory.AcceptingConnections = true;

            var policy = new TriggerableTcpConnectionPolicy();
            var progress = new AwaitableProgress<TcpSocketWriter.ProgressReport>();
            var writer = new TcpSocketWriter(null, -1, policy,
                2, socketFactory.TryOpenSocket)
            {
                Progress = progress
            };

            var p = progress.AwaitProgressAsync();
            policy.TriggerConnect();

            // Make sure we are connected
            writer.Enqueue("Event 0\r\n");
            progress.AwaitProgressAsync().Wait();
            Assert.Equal("Event 0\r\n", socketFactory.socket.GetReceivedText());

            socketFactory.socket.SocketFailed = true;
            writer.Enqueue("Event 1\r\n");
            writer.Enqueue("Event 2\r\n");
            writer.Enqueue("Event 3\r\n");
            Assert.Equal("Event 0\r\n", socketFactory.socket.GetReceivedText());

            policy.TriggerConnect();
            progress.AwaitProgressAsync().Wait();
            Assert.Equal("Event 2\r\nEvent 3\r\n", socketFactory.socket.GetReceivedText());
        }
    }
}
