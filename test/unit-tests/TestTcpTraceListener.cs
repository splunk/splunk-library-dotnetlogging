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
