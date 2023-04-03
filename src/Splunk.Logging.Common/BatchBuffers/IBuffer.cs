using System;
using System.Net.Http;

namespace Splunk.Logging.BatchBuffers
{
    public interface IBuffer : IDisposable
    {
        void Append(HttpEventCollectorEventInfo serializedEventInfo);
        long Length { get; }
        HttpContent BuildHttpContent(string mediaType);
        void SupportOriginalBehaviour();
    }
}