using Splunk.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace standalone_test
{
    // Write 10 events to a TCP input on port 10000.
    class Program
    {
        static void Main(string[] args)
        {
            HttpInput();
           
            /*
            var traceSource = new TraceSource("UnitTestLogger");
            traceSource.Listeners.Remove("Default");
            traceSource.Switch.Level = SourceLevels.All;
            traceSource.Listeners.Add(
                new TcpTraceListener(IPAddress.Loopback, 10000, 
                                     new ExponentialBackoffTcpReconnectionPolicy()));

            for (int i = 0; i < 10; i++)
                traceSource.TraceEvent(TraceEventType.Information, 100, string.Format("Boris {0}", i));
            */
        }

        static void HttpInput()
        {
            Console.WriteLine("start");
            var trace = new TraceSource("HttpInputLogger");
            trace.Switch.Level = SourceLevels.All;
            var listener = new HttpInputTraceListener();
            trace.Listeners.Add(listener);
            for (int i = 0; i < 10000; i++)
            {
                if (i % 1000 == 0) Console.WriteLine(i);
                trace.TraceEvent(TraceEventType.Information, 1, "hello world");
            }
            trace.TraceEvent(TraceEventType.Error, 2, "error");
            trace.TraceInformation("hello");
            Console.WriteLine("end");
            System.Threading.Thread.Sleep(20000);
        }
    }
}
