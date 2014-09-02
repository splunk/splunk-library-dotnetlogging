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
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Splunk.Logging;
using System.IO;

namespace Splunk.Logging
{
    public class TestTcpEventSink
    {
        [Fact]
        public void TestTcpEventSinkWrites()
        {
            var socketFactory = new MockSocketFactory();
            socketFactory.AcceptingConnections = true;
            var progress = new AwaitableProgress<TcpSocketWriter.ProgressReport>();
            var writer = new TcpSocketWriter(null, -1, new ExponentialBackoffTcpReconnectionPolicy(), 3, socketFactory.TryOpenSocket)
            {
                Progress = progress
            };
            var listener = new ObservableEventListener();

            listener.Subscribe(new TcpEventSink(writer, new TestEventFormatter()));

            var source = TestEventSource.GetInstance();
            listener.EnableEvents(source, EventLevel.LogAlways, Keywords.All);

            source.Message("Boris", "Meep");
            progress.AwaitProgressAsync().Wait();

            var result = socketFactory.socket.GetReceivedText();
            listener.Dispose();

            var expected = "EventId=1 EventName=MessageInfo Level=Error \"FormattedMessage=Meep - Boris\" \"message=Boris\" \"caller=Meep\"\r\n";
            Assert.Equal(expected, result);
        }
    }
}
