using Splunk.Logging;
using System;
using System.Net;
using System.Threading.Tasks;

namespace standalone_test
{
    /// <summary>
    /// Playground for Splunk logging .NET library.
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            await DoIt();
            Console.WriteLine("Done");
        }

        static async Task DoIt()
        {
            ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) =>
            {
                return true;
            };

            var middleware = new HttpEventCollectorResendMiddleware(100);
            var ecSender = new HttpEventCollectorSender(
                new Uri("https://localhost:8088"),
                "92A93306-354C-46A5-9790-055C688EB0C4",
                null,
                HttpEventCollectorSender.SendMode.Sequential,
                1000,
                0,
                1000,
                middleware.Plugin,
                bufferMode: HttpEventCollectorSender.BufferMode.StringBuilderBuffer);

            ecSender.OnError += o => Console.WriteLine(o.Message);

            var rnd = new Random(Environment.TickCount);
            for (var i = 0; i < 5000; i++)
            {
                for (var j = 0; j < rnd.Next(30, 50); j++)
                {
                    ecSender.Send(DateTime.UtcNow.AddDays(-1), Guid.NewGuid().ToString(), "INFO", null,
                        new
                        {
                            Foo = "Bar", test2 = "Testit2",
                            time = ConvertToEpoch(DateTime.UtcNow.AddHours(-2)).ToString(),
                            anotherkey = "anothervalue"
                        });
                    ecSender.Send(Guid.NewGuid().ToString(), "INFO", null,
                        new
                        {
                            Foo = "Bar", test2 = "Testit2",
                            time = ConvertToEpoch(DateTime.UtcNow.AddHours(-2)).ToString(),
                            anotherkey = "anothervalue!!"
                        });
                }

                Console.WriteLine(i.ToString());
            }

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