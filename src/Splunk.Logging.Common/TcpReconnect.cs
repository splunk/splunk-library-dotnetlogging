using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Splunk.Logging
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class FixedSizeQueue<T> : ConcurrentQueue<T>
    {
        public int Size { get; private set; }

        public FixedSizeQueue(int size)
        {
            Size = size;
        }

        public new void Enqueue(T obj)
        {
            base.Enqueue(obj);
            T tmp;
            lock (this)
            {
                while (base.Count > Size)
                {
                    base.TryDequeue(out tmp);
                }
            }
        }
    }

    // Interface to specify what policy should be used for reconnecting to TCP
    // sockets when there are errors.
    /// <summary>
    /// 
    /// </summary>
    public interface TcpConnectionPolicy
    {
        // A blocking method that should eventually return a Socket when it finally
        // manages to get a connection, or throw a TcpReconnectFailure if the policy
        // says to give up trying to connect.
        /// <summary>
        /// 
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Socket Reconnect(Func<Socket> connect, CancellationToken cancellationToken);
    }

    /// <summary>
    /// 
    /// </summary>
    public class ExponentialBackoffTcpConnectionPolicy : TcpConnectionPolicy
    {
        private int ceiling = 10 * 60; // 10 minutes in seconds

        public Socket Reconnect(Func<Socket> connect, CancellationToken cancellationToken)
        {
            int delay = 0; // in seconds
            while (!cancellationToken.IsCancellationRequested)
            {
                Task.Delay(delay * 1000, cancellationToken).Wait();

                try {
                    return connect();
                }
                catch (SocketException e) {}

                delay = Math.Min((delay + 1) * 2 - 1, ceiling);
            }
            return null;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class FailTcpConnectionPolicy : TcpConnectionPolicy
    {
        public Socket Reconnect(Func<Socket> connect, CancellationToken token)
        {
            throw new TcpReconnectFailure("Could not reconnect TCP port for logging. All " + 
                "subsequent log message to this listener will be dropped.");
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class TcpReconnectFailure : System.Exception
    {
        public TcpReconnectFailure(string message) : base(message) { }
    }

    /// <summary>
    /// 
    /// </summary>
    public class TcpSocketWriter : IDisposable
    {
        private FixedSizeQueue<string> eventQueue;
        private Socket socket;
        private Thread queueListener;
        private TcpConnectionPolicy connectionPolicy;
        private CancellationTokenSource tokenSource;

        public TcpSocketWriter(IPAddress host, int port, TcpConnectionPolicy policy, 
            int maxQueueSize, IProgress<EventWrittenProgressReport> progress)
        {
            this.connectionPolicy = policy;
            this.eventQueue = new FixedSizeQueue<string>(maxQueueSize);
            this.tokenSource = new CancellationTokenSource();

            var connect = new Func<Socket>(() =>
            {
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(host, port);
                return socket;
            });

            queueListener = new Thread(() =>
            {
                connect();

                while (!tokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        string entry;
                        while (eventQueue.TryDequeue(out entry))
                        {
                            this.socket.Send(Encoding.UTF8.GetBytes(entry));
                            progress.Report(new EventWrittenProgressReport {
                                Timestamp = DateTime.Now,
                                EventText = entry
                            });
                        }
                    }
                    catch (SocketException)
                    {
                        this.socket = this.connectionPolicy.Reconnect(connect, tokenSource.Token);
                    }
                }
            });
            queueListener.Start();
        }

        public void Dispose()
        {
            this.tokenSource.Cancel();
            this.socket.Close();
            this.socket.Dispose();
        }

        public void Enqueue(string entry)
        {
            this.eventQueue.Enqueue(entry);
        }
    }

    public class EventWrittenProgressReport
    {
        public DateTime Timestamp { get; set; }
        public string EventText { get; set; }
    }
}
