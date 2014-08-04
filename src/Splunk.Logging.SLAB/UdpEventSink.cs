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
    public class SimpleEventTextFormatter : IEventTextFormatter
    {
        void IEventTextFormatter.WriteEvent(EventEntry eventEntry, TextWriter writer)
        {
            writer.Write(eventEntry.GetFormattedTimestamp("o") + " ");
            writer.Write("EventId=" + eventEntry.EventId + " ");
            writer.Write("EventName=" + eventEntry.Schema.EventName + " ");
            writer.Write("Level=" + eventEntry.Schema.Level + " ");
            writer.Write("\"FormattedMessage=" + eventEntry.FormattedMessage + "\" ");
            for (int i = 0; i < eventEntry.Payload.Count; i++)
            {
                try
                {
                    writer.Write("\"{0}={1}\" ", eventEntry.Schema.Payload[i], eventEntry.Payload[i]);
                }
                catch (Exception e) { }
            }
            writer.WriteLine();
        }
    }

    public class UdpEventSink : IObserver<EventEntry>
    {
        protected Socket socket = null;
        protected IEventTextFormatter formatter;
        
        public UdpEventSink(IPAddress host, int port, IEventTextFormatter formatter = null)
        {
            this.formatter = formatter != null ? formatter : new SimpleEventTextFormatter();
            this.socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(host, port);
        }

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
            socket.Send(Encoding.UTF8.GetBytes(sw.ToString()));
        }
    }
}
