using Splunk.Logging;
using System;
using System.Net;

namespace standalone_test
{
    /// <summary>
    /// Playground for Splunk logging .NET library.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            DoIt();
            Console.ReadLine();
        }

        static async void DoIt()
        {
            ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) =>
            {
                return true;
            };

            var middleware = new HttpEventCollectorResendMiddleware(100);
            var ecSender = new HttpEventCollectorSender(new Uri("https://localhost:8088"), "92A93306-354C-46A5-9790-055C688EB0C4", null, HttpEventCollectorSender.SendMode.Sequential, 5000, 0, 0, middleware.Plugin);
            ecSender.OnError += o => Console.WriteLine(o.Message);
            ecSender.Send(DateTime.UtcNow.AddDays(-1), Guid.NewGuid().ToString(), "INFO", null, 
                new { Foo = "Bar", test2 = "Testit2", time = ConvertToEpoch(DateTime.UtcNow.AddHours(-2)).ToString(), anotherkey="anothervalue" });
            ecSender.Send(Guid.NewGuid().ToString(), "INFO", null,
                new { Foo = "Bar", test2 = "Testit2", time = ConvertToEpoch(DateTime.UtcNow.AddHours(-2)).ToString(), anotherkey = "anothervalue!!" });
            await ecSender.FlushAsync();
        }

        private static double ConvertToEpoch(DateTime date)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            TimeSpan diff = date.ToUniversalTime() - origin;
            return Math.Floor(diff.TotalSeconds);
        }

        private static void EcSender_OnError(HttpEventCollectorException obj)
        {
            throw new NotImplementedException();
        }
    }
}
