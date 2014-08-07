using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using System;
using System.IO;

namespace Splunk.Logging
{
    public class SimpleEventTextFormatter : IEventTextFormatter
    {
        void IEventTextFormatter.WriteEvent(EventEntry eventEntry, TextWriter writer)
        {
            writer.Write(eventEntry.GetFormattedTimestamp("o") + " ");
            writer.Write("EventId=" + eventEntry.EventId + " ");
            writer.Write("EventName=" + eventEntry.Schema.EventName + " ");
            writer.Write("Level=" + eventEntry.Schema.Level + " ");
            writer.Write("\"FormattedMessage=" + eventEntry.FormattedMessage + "\" ");
            for (int i = 0; i < eventEntry.Payload.Count; i++)
            {
                try
                {
                    writer.Write("\"{0}={1}\" ", eventEntry.Schema.Payload[i], eventEntry.Payload[i]);
                }
                catch (Exception) { }
            }
            writer.WriteLine();
        }
    }
}
