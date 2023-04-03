using System.IO;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

namespace Splunk.Logging.BatchBuffers
{
    public class StringBuilderBatchBuffer : IBuffer
    {
        private readonly StringBuilder builder = new StringBuilder();
        private readonly StringWriter writer;
        private readonly JsonSerializer serializer;

        public StringBuilderBatchBuffer()
        {
            writer = new StringWriter(builder);
            serializer = JsonSerializer.Create();
        }

        public void Append(HttpEventCollectorEventInfo serializedEventInfo)
        {
            serializer.Serialize(writer, serializedEventInfo);
            writer.Flush();
        }

        public long Length => builder.Length;

        public HttpContent BuildHttpContent(string mediaType)
        {
            return new StringContent(builder.ToString(), Encoding.UTF8, mediaType);
        }

        public void Dispose()
        {
        }
    }
}