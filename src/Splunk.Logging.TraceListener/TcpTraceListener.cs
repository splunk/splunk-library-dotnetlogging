using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Splunk.Logging
{
    public class TcpTraceListener : TraceListener
    {
        private TcpSocketWriter writer;
        private StringBuilder buffer = new StringBuilder();

        public TcpTraceListener(IPAddress host, int port, 
            TcpConnectionPolicy policy = null, 
            int maxQueueSize = 10000,
            IProgress<EventWrittenProgressReport> progress = null) : base()
        {
            this.writer = new TcpSocketWriter(host, port,
                policy == null ? new ExponentialBackoffTcpConnectionPolicy() : policy,
                maxQueueSize,
                progress == null ? new Progress<EventWrittenProgressReport>() : progress);
        }

        public TcpTraceListener(string host, int port,
            TcpConnectionPolicy policy = null,
            int maxQueueSize = 10000) :
            this(Dns.GetHostEntry(host).AddressList[0], port, policy, maxQueueSize) { }

        public override void Write(string message)
        {
            if (NeedIndent) WriteIndent();
            buffer.Append(message);
        }

        public override void WriteLine(string message)
        {
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
