using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Splunk.Logging.BatchBuffers
{
    public class PushStreamInMemoryBuffer : IBuffer
    {
        private readonly List<byte[]> serializedItems = new List<byte[]>();

        public void Append(HttpEventCollectorEventInfo serializedEventInfo)
        {
            serializedItems.Add( Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(serializedEventInfo)));
        }

        public long Length => serializedItems.Sum(x => x.Length);

        public HttpContent BuildHttpContent(string mediaType)
        {
            return new PushStreamContent(async s =>
            {
                foreach (var entry in serializedItems)
                {
                    await s.WriteAsync(entry, 0, entry.Length);
                }
            }, mediaType, Length);
        }

        public void SupportOriginalBehaviour()
        {
        }

        public void Dispose()
        {
        }

        private class PushStreamContent : HttpContent
        {
            private readonly Func<Stream, Task> writeContent;
            private readonly long knownLength;

            public PushStreamContent(Func<Stream, Task> writeContent, string mediaType, long knownLength)
            {
                this.writeContent = writeContent;
                this.knownLength = knownLength;
                Headers.ContentType = new MediaTypeHeaderValue(mediaType)
                {
                    CharSet = "utf-8"
                };
            }

            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                await writeContent(stream);
                await stream.FlushAsync();
            }

            protected override bool TryComputeLength(out long length)
            {
                length = this.knownLength;
                return true;
            }
        }
    }
}