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
    /// SLAB event to write to a UDP socket.
    /// </summary>
    public class UdpEventSink : IObserver<EventEntry>
    {
        private ISocket socket = null;
        private IEventTextFormatter formatter;
        
        public UdpEventSink(ISocket socket, IEventTextFormatter formatter = null)
        {
            this.socket = socket;
            this.formatter = this.formatter = formatter != null ? formatter : new SimpleEventTextFormatter();
        }

        /// <summary>
        /// Set up a sink.
        /// </summary>
        /// <param name="host">IP address to write to.</param>
        /// <param name="port">UDP port on the target machine.</param>
        /// <param name="formatter">An object controlling the formatting
        /// of the event (defaults to <code>{timestamp} EventId={...} EventName={...} Level={...} "FormattedMessage={...}"</code>).</param>
        public UdpEventSink(IPAddress host, int port, IEventTextFormatter formatter = null) :
            this(new UdpSocket(host, port), formatter) {}
        
        /// <summary>
        /// Set up a sink.
        /// </summary>
        /// <param name="host">Hostname to write to.</param>
        /// <param name="port">UDP port on the target machine.</param>
        /// <param name="formatter">An object controlling the formatting
        /// of the event (defaults to <code>{timestamp} EventId={...} EventName={...} Level={...} "FormattedMessage={...}"</code>).</param>
        public UdpEventSink(string host, int port, IEventTextFormatter formatter = null) :
            this(Dns.GetHostEntry(host).AddressList[0], port, formatter) { }

        public void OnCompleted()
        {
            socket.Close();
            socket.Dispose();
        }

        public void OnError(Exception error)
        {
            socket.Close();
            socket.Dispose();
        }

        public void OnNext(EventEntry value)
        {
            var sw = new StringWriter();
            formatter.WriteEvent(value, sw);
            socket.Send(sw.ToString());
        }
    }
}
