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
    /// <summary>
    /// SLAB event sink to write events to a TCP socket.
    /// </summary>
    public class TcpEventSink : IObserver<EventEntry>
    {
        private TcpSocketWriter writer;
        private IEventTextFormatter formatter;

        /// <summary>
        /// Get an IProgress instance that triggers when an event is pulled from
        /// the queue and written to the TCP socket.
        /// </summary>
        public IProgress<EventWrittenProgressReport> Progress { get; private set; }
        
        /// <summary>
        /// Set up a sink.
        /// </summary>
        /// <param name="host">IP address of the host to write to.</param>
        /// <param name="port">TCP port to write to.</param>
        /// <param name="formatter">An object to specify the format events should be
        /// written in (defaults to <code>{timestamp} EventId={...} EventName={...} Level={...} "FormattedMessage={...}"</code>).</param>
        /// <param name="policy">An object defining the reconnect policy in the event 
        /// of TCP errors (default: an ExponentialBackoffTcpConnectionPolicy object).</param>
        /// <param name="maxQueueSize">The maximum number of events to queue in the event of
        /// the TCP session dropping before events start to be dropped (defaults to 10,000).</param>
        /// <param name="progress">A progress reporter that triggers when events are written from
        /// the queue to the TCP port (defaults to a new Progress object). It is reachable
        /// via the Progress property.</param>
        public TcpEventSink(IPAddress host, int port, IEventTextFormatter formatter = null,
            TcpConnectionPolicy policy = null, int maxQueueSize = 10000,
            IProgress<EventWrittenProgressReport> progress = null)
        {
            this.formatter = formatter != null ? formatter : new SimpleEventTextFormatter();
            this.Progress = progress == null ? new Progress<EventWrittenProgressReport>() : progress);
            this.writer = new TcpSocketWriter(host, port, 
                policy == null ? new ExponentialBackoffTcpConnectionPolicy() : policy,
                maxQueueSize,
                this.Progress);
        }

        /// <summary>
        /// Set up a sink.
        /// </summary>
        /// <param name="host">Hostname of the host to write to.</param>
        /// <param name="port">TCP port to write to.</param>
        /// <param name="formatter">An object to specify the format events should be
        /// written in (default to <code>{timestamp} EventId={...} EventName={...} Level={...} "FormattedMessage={...}"</code>).</param>
        /// <param name="policy">An object defining the reconnect policy in the event 
        /// of TCP errors (default: an ExponentialBackoffTcpConnectionPolicy object).</param>
        /// <param name="maxQueueSize">The maximum number of events to queue in the event of
        /// the TCP session dropping before events start to be dropped (defaults to 10,000).</param>
        /// <param name="progress">A progress reporter that triggers when events are written from
        /// the queue to the TCP port (defaults to a new Progress object). It is reachable
        /// via the Progress property.</param>
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
