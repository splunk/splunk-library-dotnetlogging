using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Splunk.Logging
{
    public interface ISocket : IDisposable
    {
        void Send(string data);
        void Close();
    }

    public class UdpSocket : ISocket
    {
        private Socket socket;

        public UdpSocket(IPAddress host, int port)
        {
            this.socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
            this.socket.Connect(host, port);
        }

        public void Send(string data)
        {
            this.socket.Send(Encoding.UTF8.GetBytes(data));
        }

        public void Close() { this.socket.Close(); }
        public void Dispose() { this.socket.Dispose(); }
    }

    public class TcpSocket : ISocket
    {
        private Socket socket;

        public TcpSocket(IPAddress host, int port)
        {
            this.socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            this.socket.Connect(host, port);
        }

        public void Send(string data)
        {
            this.socket.Send(Encoding.UTF8.GetBytes(data));
        }

        public void Close() { this.socket.Close(); }
        public void Dispose() { this.socket.Dispose(); }
    }
}