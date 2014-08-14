using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Splunk.Logging
{
    public class AwaitableProgress<T> : IProgress<T>
    {
        private event Action<T> Handler = (T x) => { };

        public void Report(T value)
        {
            this.Handler(value);
        }

        public async Task<T> AwaitProgressAsync()
        {
            var source = new TaskCompletionSource<T>();
            Action<T> onReport = null;
            onReport = (T x) =>
            {
                Handler -= onReport;
                source.SetResult(x);
            };
            Handler += onReport;
            return await source.Task;
        }

        public T AwaitValue()
        {
            var task = this.AwaitProgressAsync();
            task.Wait();
            return task.Result;
        }
    }

    public class MockSocket : ISocket
    {
        private StringBuilder buffer = new StringBuilder();
        public bool SocketFailed { get; set; }

        public MockSocket() { }

        public string GetReceivedText() { return buffer.ToString(); }
        public void ClearReceiverBuffer() { buffer.Clear(); }

        public void Send(string data)
        {
            if (SocketFailed)
            {
                throw new SocketException((Int32)SocketError.ConnectionReset);
            }
            else
            {
                buffer.Append(data);
            }
        }

        public void Close() { }
        public void Dispose() { }
    }

    public class MockSocketFactory
    {
        public bool AcceptingConnections { get; set; }
        public MockSocket socket;

        public MockSocket TryOpenSocket(IPAddress host, int port)
        {
            if (AcceptingConnections)
            {
                socket = new MockSocket();
                return socket;
            }
            else
            {
                throw new SocketException((Int32)SocketError.ConnectionRefused);
            }
        }
    }
}
