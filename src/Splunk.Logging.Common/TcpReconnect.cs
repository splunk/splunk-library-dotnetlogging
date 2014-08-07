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
    /// A queue with a maximum size. When the queue is at its maximum size
    /// and a new item is queued, the oldest item in the queue is dropped.
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

    /// <summary>
    /// TcpConnectionPolicy encapsulates a policy for what logging via TCP should
    /// do when there is a socket error.
    /// </summary>
    /// <remarks>
    /// TCP loggers in this library (TcpTraceListener and TcpEventSink) take a 
    /// TcpConnectionPolicy as an argument to their constructor. When the TCP
    /// session the logger uses has an error, the logger suspends logging and calls
    /// the Reconnect method of an implementation of TcpConnectionPolicy to get a
    /// new socket.
    /// </remarks>
    public interface TcpConnectionPolicy
    {
        // A blocking method that should eventually return a Socket when it finally
        // manages to get a connection, or throw a TcpReconnectFailure if the policy
        // says to give up trying to connect.
        /// <summary>
        /// Try to reestablish a TCP connection.
        /// </summary>
        /// <remarks>
        /// The method should block until it either 
        /// 
        /// 1. succeeds and returns a connected TCP socket, or 
        /// 2. fails and throws a TcpReconnectFailure exception, or
        /// 3. the cancellationToken is cancelled, in which case the method should
        ///    return null.
        ///    
        /// The method takes a zero-parameter function that encapsulates trying to
        /// make a single connection and a cancellation token to stop the method
        /// if the logging system that invoked it is disposed.
        /// 
        /// For example, the default ExponentialBackoffTcpConnectionPolicy invokes
        /// connect after increasingly long intervals until it makes a successful
        /// connnection.
        /// </remarks>
        /// <param name="connect">A zero-parameter function that tries once to 
        /// establish a connection.</param>
        /// <param name="cancellationToken">A token used to cancel the reconnect
        /// attempt when the invoking logger is disposed.</param>
        /// <returns>A connected TCP socket.</returns>
        Socket Reconnect(Func<Socket> connect, CancellationToken cancellationToken);
    }

    /// <summary>
    /// TcpConnectionPolicy implementation that tries to reconnect after
    /// increasingly long intervals.
    /// </summary>
    /// <remarks>
    /// The intervals double every time, starting from 0s, 1s, 2s, 4s, ...
    /// until 10 minutes between connections, when it plateaus and does
    /// not increase the interval length any further.
    /// </remarks>
    public class ExponentialBackoffTcpConnectionPolicy : TcpConnectionPolicy
    {
        private int ceiling = 10 * 60; // 10 minutes in seconds

        public Socket Reconnect(Func<Socket> connect, CancellationToken cancellationToken)
        {
            int delay = 1; // in seconds
            while (!cancellationToken.IsCancellationRequested)
            {
                try {
                    return connect();
                }
                catch (SocketException e) {}

                // If this is cancelled via the cancellationToken instead of
                // completing its delay, the next while-loop test will fail,
                // the loop will terminate, and the method will return null
                // with no additional connection attempts.
                Task.Delay(delay * 1000, cancellationToken).Wait();
                // The nth delay is min(10 minutes, 2^n - 1 seconds).
                delay = Math.Min((delay + 1) * 2 - 1, ceiling);
            }
            return null;
        }
    }

    /// <summary>
    /// Exception thrown when a TcpConnectionPolicy.Reconnect method declares
    /// that is cannot get a new connection and will no longer try.
    /// </summary>
    public class TcpReconnectFailure : System.Exception
    {
        public TcpReconnectFailure(string message) : base(message) { }
    }

    /// <summary>
    /// TcpSocketWriter encapsulates queueing strings to be written to a TCP socket
    /// and handling reconnections (according to a TcpConnectionPolicy object passed
    /// to it) when a TCP session drops.
    /// </summary>
    /// <remarks>
    /// TcpSocketWriter maintains a fixed sized queue of strings to be sent via
    /// the TCP port and, while the socket is open, sends them as quickly as possible.
    /// 
    /// If the TCP session drops, TcpSocketWriter will stop pulling strings off the
    /// queue until it can reestablish a connection.
    /// </remarks>
    public class TcpSocketWriter : IDisposable
    {
        private FixedSizeQueue<string> eventQueue;
        private Socket socket;
        private Thread queueListener;
        private TcpConnectionPolicy connectionPolicy;
        private CancellationTokenSource tokenSource;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="host">IPAddress of the host to open a TCP socket to.</param>
        /// <param name="port">TCP port to use on the target host.</param>
        /// <param name="policy">A TcpConnectionPolicy object defining reconnect behavior.</param>
        /// <param name="maxQueueSize">The maximum number of log entries to queue before starting to drop entries.</param>
        /// <param name="progress">An IProgress object that reports when entries are 
        /// pulled off the queue and written to the TCP socket.</param>
        public TcpSocketWriter(IPAddress host, int port, TcpConnectionPolicy policy, 
            int maxQueueSize, IProgress<EventWrittenProgressReport> progress)
        {
            this.connectionPolicy = policy;
            this.eventQueue = new FixedSizeQueue<string>(maxQueueSize);
            this.tokenSource = new CancellationTokenSource();

            // Function encapsulating connecting to a TCP socket.
            var connect = new Func<Socket>(() =>
            {
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(host, port);
                return socket;
            });

            queueListener = new Thread(() =>
            {
                this.socket = connect();

                string entry = null;
                while (!tokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
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
                        this.socket.Send(Encoding.UTF8.GetBytes(entry));
                        progress.Report(new EventWrittenProgressReport
                        {
                            Timestamp = DateTime.Now,
                            EventText = entry
                        });
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

        /// <summary>
        /// Push a string onto the queue to be written.
        /// </summary>
        /// <param name="entry">The string to be written to the TCP socket.</param>
        public void Enqueue(string entry)
        {
            this.eventQueue.Enqueue(entry);
        }
    }

    /// <summary>
    /// Class used for reporting progress by TcpSocketWriter.
    /// </summary>
    public class EventWrittenProgressReport
    {
        public DateTime Timestamp { get; set; }
        public string EventText { get; set; }
    }
}
