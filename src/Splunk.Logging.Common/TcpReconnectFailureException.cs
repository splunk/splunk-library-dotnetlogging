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

namespace Splunk.Logging
{
    /// <summary>
    /// Exception thrown when a TcpConnectionPolicy.Reconnect method declares
    /// that is cannot get a new connection and will no longer try.
    /// </summary>
    public class TcpReconnectFailureException : System.Exception
    {
        public TcpReconnectFailureException(string message) : base(message) { }
    }
}
