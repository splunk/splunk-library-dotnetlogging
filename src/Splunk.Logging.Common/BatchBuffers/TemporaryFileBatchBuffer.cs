using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;

namespace Splunk.Logging.BatchBuffers
{
    public class TemporaryFileBatchBuffer : IBuffer
    {
        private static readonly string TempDir = Path.GetTempPath();
        private readonly string filePath;
        private readonly JsonSerializer serializer;
        private readonly TextWriter writer;
        private readonly FileStream fileStream;

        public TemporaryFileBatchBuffer()
        {
            filePath = Path.GetFileName(Path.GetTempFileName());
            serializer = JsonSerializer.Create();
            fileStream = File.OpenWrite($"{TempDir}{filePath}");
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
            var mediaTypeHeaderValue = new MediaTypeHeaderValue(mediaType)
            {
                CharSet = "utf-8"
            };
            return new StreamContent(File.OpenRead($"{TempDir}{filePath}"))
            {
                Headers =
                {
                    ContentType = mediaTypeHeaderValue
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
                File.Delete($"{TempDir}{filePath}");
            }
            catch (Exception)
            {
                //Ignore
            }
        }
    }
}