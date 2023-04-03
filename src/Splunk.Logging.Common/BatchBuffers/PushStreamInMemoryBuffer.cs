using System;
using System.Collections.Generic;
using System.IO;
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
        private readonly List<HttpEventCollectorEventInfo> events;

        public PushStreamInMemoryBuffer(List<HttpEventCollectorEventInfo> events)
        {
            this.events = events;
        }

        public void Append(HttpEventCollectorEventInfo serializedEventInfo)
        {
        }

        public long Length =>
            events.Count * 3 * 1024; //assume 3kb for a log entry. Not ideal, but we can get more finessed if we want.

        public HttpContent BuildHttpContent(string mediaType)
        {
            return new PushStreamContent(async s =>
            {
                foreach (var evt in this.events)
                {
                    var entry = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(evt));
                    await s.WriteAsync(entry, 0, entry.Length);
                }
            }, mediaType);
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

            public PushStreamContent(Func<Stream, Task> writeContent, string mediaType)
            {
                this.writeContent = writeContent;
                Headers.ContentType = new MediaTypeHeaderValue(mediaType);
            }

            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                await writeContent(stream);
                await stream.FlushAsync();
            }

            protected override bool TryComputeLength(out long length)
            {
                length = -1;
                return false;
            }
        }
    }
}