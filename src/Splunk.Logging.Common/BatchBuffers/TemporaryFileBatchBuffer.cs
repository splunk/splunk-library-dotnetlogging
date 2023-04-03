using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;

namespace Splunk.Logging.BatchBuffers
{
    public class TemporaryFileBatchBuffer : IBuffer
    {
        private static readonly string tempDir = Path.GetTempPath();
        private readonly string filePath;
        private readonly JsonSerializer serializer;
        private readonly TextWriter writer;
        private readonly FileStream fileStream;

        public TemporaryFileBatchBuffer()
        {
            filePath = Path.GetFileName(Path.GetTempFileName());
            serializer = JsonSerializer.Create();
            fileStream = File.OpenWrite($"{tempDir}{filePath}");
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
            return new StreamContent(File.OpenRead($"{tempDir}{filePath}"))
            {
                Headers =
                {
                    ContentType = new MediaTypeHeaderValue(mediaType)
                }
            };
        }

        public void SupportOriginalBehaviour()
        {
        }

        public void Dispose()
        {
            writer?.Dispose();
            fileStream?.Dispose();
            try
            {
                File.Delete($"{tempDir}{filePath}");
            }
            catch (Exception)
            {
                //Ignore
            }
        }
    }
}