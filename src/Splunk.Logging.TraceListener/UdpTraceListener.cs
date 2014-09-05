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
    /// Write event traces to a UDP port.
    /// </summary>
    public class UdpTraceListener : TraceListener
    {
        private Socket socket;
        private StringBuilder buffer = new StringBuilder();

        public UdpTraceListener(Socket socket) : base()
        {
            this.socket = socket;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="host">IP address to write to.</param>
        /// <param name="port">UDP port to log to on the remote host.</param>
        public UdpTraceListener(IPAddress host, int port) : 
            this(Util.OpenUdpSocket(host, port)) { }
            
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="host">Hostname to write to.</param>
        /// <param name="port">UDP port to log to on the remote host.</param>
        public UdpTraceListener(string host, int port) :
            this(host.HostnameToIPAddress(), port) { }

        public override void Write(string message)
        {
            // Note: not thread-safe, since the threading is handled by the TraceListener machinery that
            // invokes this method.
            if (NeedIndent)
                WriteIndent();
            socket.Send(Encoding.UTF8.GetBytes(message));
        }

        // This is factored out so it can be overridden in the test suite.
        protected virtual string GetTimestamp()
        {
            return DateTime.UtcNow.ToLocalTime().ToString("o");
        }

        public override void WriteLine(string message)
        {
            // Note: not thread-safe, since the threading is handled by the TraceListener machinery that
            // invokes this method.
            if (NeedIndent) WriteIndent();
            socket.Send(Encoding.UTF8.GetBytes(message + Environment.NewLine));
        }

        public override void Close()
        {
            socket.Close();
            socket.Dispose();
            base.Close();
        }
    }
}
