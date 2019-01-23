using Splunk.Logging;
using System;
using System.Diagnostics;
namespace examples
{
    class Program
    {
        static void Main(string[] args)
        {
            EnableSelfSignedCertificates();

            TraceListenerExample();
        }

        private static void TraceListenerExample()
        {
            // Replace with your HEC token
            string token = "1ff51387-405a-4566-9f6a-87bc3fb44424";

            // TraceListener
            var trace = new TraceSource("demo-logger");
            trace.Switch.Level = SourceLevels.All;
            var listener = new HttpEventCollectorTraceListener(
                uri: new Uri("https://127.0.0.1:8088"),
                token: token,
                batchSizeCount: 1);
            trace.Listeners.Add(listener);

            // Send some events
            trace.TraceEvent(TraceEventType.Error, 0, "hello world 0");
            trace.TraceEvent(TraceEventType.Information, 1, "hello world 1");
            trace.TraceData(TraceEventType.Information, 2, "hello world 2");
            trace.TraceData(TraceEventType.Error, 3, "hello world 3");
            trace.TraceData(TraceEventType.Information, 4, "hello world 4");
            
        
            trace.Flush();
            trace.Close();
            System.Threading.Thread.Sleep(500);

            Console.WriteLine("helloworld");
            // Now search splunk index that used by your HEC token you should see above 5 events are indexed
        }

        private static void EnableSelfSignedCertificates()
        {
            // Enable self signed certificates
            System.Net.ServicePointManager.ServerCertificateValidationCallback +=
                delegate (object sender, System.Security.Cryptography.X509Certificates.X509Certificate certificate,
                    System.Security.Cryptography.X509Certificates.X509Chain chain,
                    System.Net.Security.SslPolicyErrors sslPolicyErrors)
                {
                    return true;
                };
        }
    }
}
