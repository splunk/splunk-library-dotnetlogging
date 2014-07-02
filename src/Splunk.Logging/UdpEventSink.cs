using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Splunk.Logging
{
    public interface EventEntryFormatter
    {
        string Format(EventEntry entry);
    }

    public class SimpleEventEntryFormatter : EventEntryFormatter
    {
        public string Format(EventEntry entry)
        {
            var buffer = new StringBuilder();


            buffer.Append(entry.GetFormattedTimestamp("o") + " ");
            buffer.Append("EventId=" + entry.EventId + " ");
            buffer.Append("EventName=" + entry.Schema.EventName + " ");
            buffer.Append("Level=" + entry.Schema.Level + " ");
            buffer.Append("\"FormattedMessage=" + entry.FormattedMessage + "\" ");
            for (int i = 0; i < entry.Payload.Count; i++)
            {
                try
                {
                    buffer.AppendFormat("\"{0}={1}\" ", entry.Schema.Payload[i], entry.Payload[i]);
                }
                catch (Exception e) { }
            }

            return buffer.ToString();
        }
    }

    public class UdpEventSink : IObserver<EventEntry>
    {
        private Socket socket;
        private EventEntryFormatter formatter;
        
        public UdpEventSink(IPAddress host, int port, EventEntryFormatter formatter = null)
        {
            this.formatter = formatter != null ? formatter : new SimpleEventEntryFormatter();
            socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(host, port);
        }

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
            var eventText = formatter.Format(value);
            socket.Send(Encoding.UTF8.GetBytes(eventText));
        }
    }
}
