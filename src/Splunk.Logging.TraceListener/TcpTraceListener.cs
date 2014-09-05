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
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Splunk.Logging
{
    /// <summary>
    /// Send trace events to a TCP port.
    /// </summary>
    public class TcpTraceListener : TraceListener
    {
        private TcpSocketWriter writer;
        private StringBuilder buffer = new StringBuilder();

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="host">IP address to log to.</param>
        /// <param name="port">Port on remote host.</param>
        /// <param name="policy">An object embodying a reconnection policy for if the 
        /// TCP session drops (defaults to ExponentialBackoffTcpConnectionPolicy).</param>
        /// <param name="maxQueueSize">The maximum number of events to queue if the 
        /// TCP session goes down. If more events are queued, old ones will be dropped
        /// (defaults to 10,000).</param>
        /// <param name="progress">An IProgress instance that will be triggered when
        /// an event is pulled from the queue and written to the TCP port (defaults to a new
        /// Progress object accessible via the Progress property).</param>
        public TcpTraceListener(IPAddress host, int port, ITcpReconnectionPolicy policy, 
                                int maxQueueSize = 10000) : base()
        {
            this.writer = new TcpSocketWriter(host, port, policy, maxQueueSize);
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="host">Hostname to log to.</param>
        /// <param name="port">Port on remote host.</param>
        /// <param name="policy">An object embodying a reconnection policy for if the 
        /// TCP session drops (defaults to ExponentialBackoffTcpConnectionPolicy).</param>
        /// <param name="maxQueueSize">The maximum number of events to queue if the 
        /// TCP session goes down. If more events are queued, old ones will be dropped
        /// (defaults to 10,000).</param>
        /// <param name="progress">An IProgress instance that will be triggered when
        /// an event is pulled from the queue and written to the TCP port (defaults to a new
        /// Progress object accessible via the Progress property).</param>
        public TcpTraceListener(string host, int port, ITcpReconnectionPolicy policy,
                                int maxQueueSize = 10000) :
            this(host.HostnameToIPAddress(), port, policy, maxQueueSize) { }

        /// <summary>
        /// Add a handler to be invoked when exceptions are thrown during the operation of
        /// TCP logging, in particular when SocketErrors cause reconnect attempts or when
        /// the reconnect policy gives up entirely.
        /// </summary>
        /// <param name="handler">A function to handle the exception.</param>
        public void AddLoggingFailureHandler(Action<Exception> handler)
        {
            this.writer.LoggingFailureHandler += handler;
        }

        public override void Write(string message)
        {
            // Note: not thread-safe, since the threading is handled by the TraceListener machinery that
            // invokes this method.
            if (NeedIndent) WriteIndent();
            buffer.Append(message);
        }

        public override void WriteLine(string message)
        {
            // Note: not thread-safe, since the threading is handled by the TraceListener machinery that
            // invokes this method.
            if (NeedIndent) WriteIndent();

            buffer.Insert(0, DateTime.UtcNow.ToLocalTime().ToString("o") + " ");
            buffer.Append(message);
            buffer.Append(Environment.NewLine);

            writer.Enqueue(buffer.ToString());
            buffer.Clear();
        }

        public override void Close()
        {
            writer.Dispose();
            base.Close();
        }
    }
}
