using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace Splunk.Logging.BatchBuffers
{
    public class MemoryStreamBatchBuffer : IBuffer
    {
        private readonly string filePath;
        private readonly JsonSerializer serializer;
        private readonly TextWriter writer;
        private readonly FileStream fileStream;

        public MemoryStreamBatchBuffer()
        {
            filePath = Path.GetTempFileName();
            serializer = JsonSerializer.Create();
            fileStream = File.OpenWrite(filePath);
            writer = new StreamWriter(fileStream);
        }

        public void Append(HttpEventCollectorEventInfo serializedEventInfo)
        {
            serializer.Serialize(writer, serializedEventInfo);
            writer.Flush();
        }

        public long Length => fileStream.Length;

        public HttpContent BuildHttpContent(string mediaType)
        {
            writer.Flush();
            writer.Close();
            return new StreamContent(File.OpenRead(filePath))
            {
                Headers =
                {
                    ContentType = new MediaTypeHeaderValue(mediaType)
                }
            };
        }

        public void Dispose()
        {
            writer?.Dispose();
            fileStream?.Dispose();
            try
            {
                File.Delete(filePath);
            }
            catch (Exception)
            {
                //Ignore
            }
        }
    }
}