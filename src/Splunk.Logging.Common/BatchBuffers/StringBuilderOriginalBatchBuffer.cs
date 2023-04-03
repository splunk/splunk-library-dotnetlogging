using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

namespace Splunk.Logging.BatchBuffers
{
    public class StringBuilderOriginalBatchBuffer : IBuffer
    {
        private readonly StringBuilder builder = new StringBuilder();
        private string serializedEvents;

        public void Append(HttpEventCollectorEventInfo serializedEventInfo)
        {
            builder.Append(JsonConvert.SerializeObject(serializedEventInfo));
        }

        public long Length => builder.Length;

        public HttpContent BuildHttpContent(string mediaType)
        {
            return new StringContent(serializedEvents, Encoding.UTF8, mediaType);
        }

        public void SupportOriginalBehaviour()
        {
            this.serializedEvents = builder.ToString();
        }

        public void Dispose()
        {
        }
    }
}