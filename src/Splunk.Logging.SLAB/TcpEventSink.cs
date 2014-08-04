using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Splunk.Logging
{
    public class TcpEventSink : IObserver<EventEntry>
    {
        private TcpSocketWriter writer;
        private IEventTextFormatter formatter;
        
        public TcpEventSink(IPAddress host, int port, IEventTextFormatter formatter = null,
            TcpConnectionPolicy policy = null, int maxQueueSize = 10000,
            IProgress<EventWrittenProgressReport> progress = null)
        {
            this.formatter = formatter != null ? formatter : new SimpleEventTextFormatter();
            this.writer = new TcpSocketWriter(host, port, 
                policy == null ? new ExponentialBackoffTcpConnectionPolicy() : policy,
                maxQueueSize,
                progress == null ? new Progress<EventWrittenProgressReport>() : progress);
        }

        public TcpEventSink(string host, int port, IEventTextFormatter formatter = null,
            TcpConnectionPolicy policy = null, int maxQueueSize = 10000,
            IProgress<EventWrittenProgressReport> progress = null) :
            this(Dns.GetHostEntry(host).AddressList[0], port,
                formatter, policy, maxQueueSize, progress) { }

        public void OnCompleted()
        {
            this.writer.Dispose();
        }

        public void OnError(Exception error)
        {
            this.writer.Dispose();
        }

        public void OnNext(EventEntry value)
        {
            var sw = new StringWriter();
            formatter.WriteEvent(value, sw);
            this.writer.Enqueue(sw.ToString());
        }
    }
}
