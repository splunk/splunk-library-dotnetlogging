using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Splunk.Logging
{
    public class UdpTraceListener : TraceListener
    {
        private Socket socket;
        private StringBuilder buffer = new StringBuilder();

        public UdpTraceListener(IPAddress host, int port)
            : base()
        {
            socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(host, port);
        }

        public UdpTraceListener(string host, int port) :
            this(Dns.GetHostEntry(host).AddressList[0], port) { }

        public override void Write(string message)
        {
            if (NeedIndent)
                WriteIndent();

            buffer.Append(message);
        }

        public override void WriteLine(string message)
        {
            if (NeedIndent)
                WriteIndent();

            buffer.Insert(0, DateTime.UtcNow.ToLocalTime().ToString("o") + " ");
            buffer.Append(message);
            buffer.Append(Environment.NewLine);

            socket.Send(Encoding.UTF8.GetBytes(buffer.ToString()));
            buffer.Clear();
        }

        public override void Close()
        {
            socket.Close();
            socket.Dispose();
            base.Close();
        }
    }
}
