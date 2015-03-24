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
            System.Net.ServicePointManager.ServerCertificateValidationCallback +=
                delegate(object sender, System.Security.Cryptography.X509Certificates.X509Certificate certificate,
                    System.Security.Cryptography.X509Certificates.X509Chain chain,
                    System.Net.Security.SslPolicyErrors sslPolicyErrors)
            {
                return true; 
            };

            Console.WriteLine("start");
            var trace = new TraceSource("HttpInputLogger");
            trace.Switch.Level = SourceLevels.All;
            var meta = new Dictionary<string, string>();
            meta["index"] = "main";
            meta["source"] = "host";
            meta["sourcetype"] = "log";
            var listener = new HttpInputTraceListener(
                uri: "https://oizmerly-mbp:8089", 
                token: "E6099437-3E1F-4793-90AB-0E5D9438A918",
                metadata: meta,
                batchInterval: 1000);
            trace.Listeners.Add(listener);
            string[] data =  {"hello", "world"};
            trace.TraceData(TraceEventType.Error, 2, data);
            for (int i = 0; i < 10; i++)
            {
                if (i % 100 == 0) Console.WriteLine(i);
                trace.TraceEvent(TraceEventType.Information, 1, "hello world");
            }
            trace.TraceEvent(TraceEventType.Error, 2, "error");
            trace.TraceInformation("hello");
            Console.WriteLine("end");
            System.Threading.Thread.Sleep(200000);
        }
    }
}
