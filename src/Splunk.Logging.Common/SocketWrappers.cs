/*
 * Copyright 2014 Splunk, Inc.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"): you may
 * not use this file except in compliance with the License. You may obtain
 * a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 */
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

// .NET's Socket class is neither subclassable nor implements any interfaces,
// so to mock it for testing purposes, we have to provide a wrapper. The wrappers
// all implement the ISocket interface. There are two implementations, a TcpSocket
// and a UdpSocket, used in the TCP and UDP logging classes, respectively.
//
// These socket wrappers are write only, since for a logging framework there is
// never a reason to read.
namespace Splunk.Logging
{
    /// <summary>
    /// Interface to describe a socket.
    /// </summary>
    public interface ISocket : IDisposable
    {
        void Send(string data);
        void Close();
    }

    /// <summary>
    /// Wrapper around a .NET Socket object that sends datagrams over UDP.
    /// </summary>
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

    /// <summary>
    /// Wrapper around a .NET Socket object that sends a stream over TCP.
    /// </summary>
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