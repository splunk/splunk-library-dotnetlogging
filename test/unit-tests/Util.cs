/*
 * Copyright 2014 Splunk, Inc.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"): you may
 * not use this file except in compliance with the License. You may obtain
 * a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 */
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using System;
using System.Diagnostics.Tracing;
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
        public void Message(string message, string caller)
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

    public class AwaitableProgress<T> : IProgress<T>
    {
        private event Action<T> Handler = (T x) => { };

        public void Report(T value)
        {
            this.Handler(value);
        }

        public async Task<T> AwaitProgressAsync()
        {
            var source = new TaskCompletionSource<T>();
            Action<T> onReport = null;
            onReport = (T x) =>
            {
                Handler -= onReport;
                source.SetResult(x);
            };
            Handler += onReport;
            return await source.Task;
        }

        public T AwaitValue()
        {
            var task = this.AwaitProgressAsync();
            task.Wait();
            return task.Result;
        }
    }

    public static class ExtensionMethods
    {
        public static async Task AwaitValue(this AwaitableProgress<int> progress, int value)
        {
            int reached;
            do
            {
                reached = await progress.AwaitProgressAsync();
            } while (reached < value);
        }
    }

    public class TestEventFormatter : IEventTextFormatter
    {
        public void WriteEvent(EventEntry eventEntry, System.IO.TextWriter writer)
        {
            writer.Write("EventId=" + eventEntry.EventId + " ");
            writer.Write("EventName=" + eventEntry.Schema.EventName + " ");
            writer.Write("Level=" + eventEntry.Schema.Level + " ");
            writer.Write("\"FormattedMessage=" + eventEntry.FormattedMessage + "\" ");
            for (int i = 0; i < eventEntry.Payload.Count; i++)
            {
                try
                {
                    if (i != 0) writer.Write(" ");
                    writer.Write("\"{0}={1}\"", eventEntry.Schema.Payload[i], eventEntry.Payload[i]);
                }
                catch (Exception) { }
            }
            writer.WriteLine();
        }
    }
}
