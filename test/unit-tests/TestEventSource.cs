using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Splunk.Logging
{
    [EventSource(Name = "TestEventSource")]
    public class TestEventSource : EventSource
    {
        private static TestEventSource instance = null;

        public class Keywords
        {
        }

        public class Tasks
        {
        }

        [Event(1, Message = "{1} - {0}", Level = EventLevel.Error)]
        internal void Message(string message, string caller)
        {
            this.WriteEvent(1, message, caller);
        }

        public static TestEventSource GetInstance()
        {
            if (instance == null)
            {
                instance = new TestEventSource();
            }
            return instance;
        }
    }
}
