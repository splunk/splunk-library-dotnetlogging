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
        /// An IProgress instance that triggers when an event is pulled from the queue
        /// and written to a TCP port.
        /// </summary>
        public IProgress<EventWrittenProgressReport> Progress { get; private set; }

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
        public TcpTraceListener(IPAddress host, int port, 
            TcpConnectionPolicy policy = null, 
            int maxQueueSize = 10000,
            IProgress<EventWrittenProgressReport> progress = null) : base()
        {
            this.Progress = progress == null ? new Progress<EventWrittenProgressReport>() : progress;
            this.writer = new TcpSocketWriter(host, port,
                policy == null ? new ExponentialBackoffTcpConnectionPolicy() : policy,
                maxQueueSize,
                Progress);
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
