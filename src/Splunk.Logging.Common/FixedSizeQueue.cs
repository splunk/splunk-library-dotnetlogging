using System;
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
using System.Collections.Concurrent;

namespace Splunk.Logging
{
    /// <summary>
    /// A queue with a maximum size. When the queue is at its maximum size
    /// and a new item is queued, the oldest item in the queue is dropped.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class FixedSizeQueue<T> : ConcurrentQueue<T>
    {
        public int Size { get; private set; }
        public IProgress<bool> Progress = new Progress<bool>();
        public bool IsCompleted { get; private set; }

        public FixedSizeQueue(int size)
        {
            Size = size;
            IsCompleted = false;
        }

        public new void Enqueue(T obj)
        {
            lock (this)
            {
                if (IsCompleted)
                {
                    throw new InvalidOperationException("Tried to add an item to a completed queue.");
                }
        
                base.Enqueue(obj);
                T tmp;

                while (base.Count > Size)
                {
                    base.TryDequeue(out tmp);
                }
                Progress.Report(true);
            }
        }

        public void CompleteAdding()
        {
            lock (this)
            {
                IsCompleted = true;
            }
        }
    }
}
