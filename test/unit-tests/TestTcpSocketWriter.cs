/*
 * Copyright 2014 Splunk, Inc.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"): you may
 * not use this file except in compliance with the License. You may obtain
 * a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Splunk.Logging
{
    public class TestTcpSocketWriter
    {
        class TryOnceTcpConnectionPolicy : TcpReconnectionPolicy
        {
            public Socket Connect(Func<System.Net.IPAddress, int, Socket> connect, 
                    System.Net.IPAddress host, int port, 
                    System.Threading.CancellationToken cancellationToken)
            {
                try
                {
                    // In a proper implementation, we would check for cancellation here
                    // and return null if cancellationToken is cancelled. However, we want
                    // to use Dispose on a TcpSocketWriter to block until all activity
                    // has ended and still get a TcpReconnectFailureException.
                    return connect(host, port);
                }
                catch (SocketException e)
                {
                    throw new TcpReconnectFailureException("Reconnect failed: " + e.Message);
                }
            }
        }

        [Fact]
        public async Task TestReconnectFailure()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.Server.LocalEndPoint).Port;
            
            var writer = new TcpSocketWriter(IPAddress.Loopback, port, new TryOnceTcpConnectionPolicy(), 2);
            var receiver = listener.AcceptTcpClient();
            receiver.Close();
            
            var errors = new List<Exception>();
            var errorThrown = false;
            writer.LoggingFailureHandler += (ex) => {
                errorThrown = true;
                errors.Add(ex); 
            };
            listener.Stop();

            while (!errorThrown)
            {
                writer.Enqueue("boris\r\n");
            }
            writer.Dispose();

            Assert.Equal(3, errors.Count());
            Assert.True(errors[0] is SocketException);
            Assert.True(errors[1] is SocketException);
            Assert.True(errors[2] is TcpReconnectFailureException);
        }

        [Fact]
        public async Task TestEventsQueuedWhileWaitingForInitialConnection()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.Server.LocalEndPoint).Port;

            var writer = new TcpSocketWriter(IPAddress.Loopback, port, new ExponentialBackoffTcpReconnectionPolicy(), 100);

            writer.Enqueue("Event 1\r\n");
            writer.Enqueue("Event 2\r\n");

            listener.Start();
            var listenerClient = listener.AcceptTcpClient();
            var receiverReader = new StreamReader(listenerClient.GetStream());

            Assert.Equal("Event 1", await receiverReader.ReadLineAsync());
            Assert.Equal("Event 2", await receiverReader.ReadLineAsync());

            listener.Stop();
            listenerClient.Close();
            writer.Dispose();
        }

        public class TriggeredTcpReconnectionPolicy : TcpReconnectionPolicy
        {
            private AutoResetEvent trigger = new AutoResetEvent(false);
            public Socket Connect(Func<IPAddress,int,Socket> connect, IPAddress host, int port, CancellationToken cancellationToken)
            {
 	            while (true)
                {
                    try
                    {
                        trigger.WaitOne();
                        return connect(host, port);
                    }
                    catch (SocketException) {
                        Thread.Sleep(150);
                    }
                }
            }

            public void Trigger()
            {
                trigger.Set();
            }
        }

        [Fact]
        public async Task TestEventsQueuedCanBeDropped()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.Server.LocalEndPoint).Port;

            var policy = new TriggeredTcpReconnectionPolicy();
            policy.Trigger();
            var writer = new TcpSocketWriter(IPAddress.Loopback, port, policy, 2);

            var listenerClient = await listener.AcceptTcpClientAsync();
            listenerClient.Close();

            var errors = new List<Exception>();
            var errorThrown = false;
            writer.LoggingFailureHandler += (ex) =>
            {
                errorThrown = true;
                errors.Add(ex);
            };

            while (!errorThrown)
            {
                writer.Enqueue("boris\r\n");
            }
            for (int i = 0; i < 10; i++)
            {
                writer.Enqueue("boris\r\n");
            }

            policy.Trigger();
            listenerClient = await listener.AcceptTcpClientAsync();
            
            writer.Dispose();

            var receiverReader = new StreamReader(listenerClient.GetStream());

            // Then check what was left in the queue when we disconnected
            Assert.Equal("boris", await receiverReader.ReadLineAsync());
            Assert.Equal("boris", await receiverReader.ReadLineAsync());
            Assert.Equal(0, listenerClient.Available);
        }
    }
}
