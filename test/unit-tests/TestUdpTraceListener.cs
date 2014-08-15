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