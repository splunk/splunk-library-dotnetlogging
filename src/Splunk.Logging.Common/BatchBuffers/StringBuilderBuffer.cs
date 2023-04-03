using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

namespace Splunk.Logging.BatchBuffers
{
    public class StringBuilderBuffer : IBuffer
    {
        private readonly StringBuilder builder = new StringBuilder();

        public void Append(HttpEventCollectorEventInfo serializedEventInfo)
        {
            builder.Append(JsonConvert.SerializeObject(serializedEventInfo));
        }

        public long Length => builder.Length;

        public HttpContent BuildHttpContent(string mediaType)
        {
            return new StringContent(builder.ToString(), Encoding.UTF8, mediaType);
        }

        public void SupportOriginalBehaviour()
        {
        }

        public void Dispose()
        {
        }
    }
}