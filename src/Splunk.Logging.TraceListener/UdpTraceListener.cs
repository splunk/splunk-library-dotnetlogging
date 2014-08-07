using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Splunk.Logging
{
    /// <summary>
    /// Write event traces to a UDP port.
    /// </summary>
    public class UdpTraceListener : TraceListener
    {
        private Socket socket;
        private StringBuilder buffer = new StringBuilder();

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="host">IP address to write to.</param>
        /// <param name="port">UDP port to log to on the remote host.</param>
        public UdpTraceListener(IPAddress host, int port)
            : base()
        {
            socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(host, port);
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="host">Hostname to write to.</param>
        /// <param name="port">UDP port to log to on the remote host.</param>
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
