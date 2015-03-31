using Splunk.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using System.Diagnostics.Tracing;

namespace standalone_test
{
    // Write 10 events to a TCP input on port 10000.
    class Program
    {
        static void Main(string[] args)
        {
            System.Net.ServicePointManager.ServerCertificateValidationCallback +=
                delegate(object sender, System.Security.Cryptography.X509Certificates.X509Certificate certificate,
                    System.Security.Cryptography.X509Certificates.X509Chain chain,
                    System.Net.Security.SslPolicyErrors sslPolicyErrors)
            {
                return true; 
            };           
            var traceSource = new TraceSource("UnitTestLogger");
            traceSource.Listeners.Remove("Default");
            traceSource.Switch.Level = SourceLevels.All;
            traceSource.Listeners.Add(
                new TcpTraceListener(IPAddress.Loopback, 10000, 
                                     new ExponentialBackoffTcpReconnectionPolicy()));

            for (int i = 0; i < 10; i++)
                traceSource.TraceEvent(TraceEventType.Information, 100, string.Format("Boris {0}", i));
        }
    }
}
