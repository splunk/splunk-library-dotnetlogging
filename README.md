# Splunk logging for .NET 

#### Version 1.7.0

Splunk logging for .NET enables you to configure [HTTP Event Collector](http://dev.splunk.com/view/event-collector/SP-CAAAE6M), UDP or TCP 
logging of events to a Splunk Enterprise instance from within your .NET 
applications, via a .NET TraceListener or a Semantic Logging Application Block
(SLAB) event sink.

Each library consists of several extensions for existing .NET logging 
frameworks. Specifically, there are two libraries available, along with a third
common library that is required by both main libraries:

* `Splunk.Logging.TraceListener` 
* `Splunk.Logging.SLAB` 
* `Splunk.Logging.Common`

## Get started 

The information in this Readme provides steps to get going quickly, but for
more in-depth information be sure to visit [Splunk logging for
.NET](http://dev.splunk.com/view/splunk-loglib-dotnet/SP-CAAAEX4) page on 
[Splunk Developer Portal](http://dev.splunk.com).

### Requirements

Here's what you need to use Splunk logging for .NET:

* **.NET Framework 4.0 or later**: Splunk logging for .NET 
  requires the .NET Framework version 4.0 or later.
* **Splunk Enterprise** or **Splunk Cloud**: If you haven't already installed Splunk Enterprise,
  download it at [http://www.splunk.com/download](http://www.splunk.com/download). Otherwise, you
  should have at least a trial subscription to [Splunk Cloud](http://www.splunkcloud.com).

If you want to build the libraries and run the test suite, you will also
need:

* **xUnit runner**: If you use ReSharper, install its 
  [xUnit.net Test Support](https://resharper-plugins.jetbrains.com/packages/xunitcontrib/). 
  Otherwise, install the [xUnit.net runner for Visual Studio 2012 and 2013](http://visualstudiogallery.msdn.microsoft.com/463c5987-f82b-46c8-a97e-b1cde42b9099).
* **Visual Studio**: Splunk logging for .NET supports 
  development in [Microsoft Visual Studio 2012 or later](http://www.microsoft.com/visualstudio/downloads).

### Install

You have several options for installing Splunk logging for .NET.
The most common method is through NuGet. Add the package you want after 
searching for "splunk" in the Manage NuGet Packages window in Visual Studio.

For more information, and for information about other ways to install 
Splunk logging for .NET, see [Install Splunk logging
for .NET](http://dev.splunk.com/view/splunk-loglib-dotnet/SP-CAAAEYC)


#### Solution layout

The solution is organized into `src` and `test` directories. The `src`
directory contains three libraries: `Splunk.Logging.TraceListener` (which 
contains [.NET trace listeners](http://msdn.microsoft.com/library/4y5y10s7)
that log events to Splunk Enterprise over UDP or TCP), `Splunk.Logging.SLAB`
(which contains [Semantic Logging Application Block (SLAB) event sinks](http://msdn.microsoft.com/library/dn440729#sec29)
that log ETW events to Splunk Enterprise over UDP or TCP), and 
`Splunk.Logging.Common` (a common library that contains resources required by
both logging libraries). The `test` directory contains a single project,
`unit-tests`.

#### Examples and unit tests

Splunk logging for .NET include full unit tests which run 
using [xunit](https://github.com/xunit/xunit).

### Example code

#### Add logging to Splunk via a TraceListener

Below is a snippet showing creating a `TraceSource` and then attaching a
`UdpTraceListener` (or `TcpTraceListener`) configured to talk to localhost
on port 10000. Next an event is generated which is sent to Splunk.

```csharp
//setup
var traceSource = new TraceSource("TestLogger");
traceSource.Listeners.Remove("Default");
traceSource.Switch.Level = SourceLevels.All;
traceSource.Listeners.Add(new UdpTraceListener(IPAddress.Loopback, 10000));
// or, for TCP:
// traceSource.Listeners.Add(new TcpTraceListener(IPAddress.Loopback, 10000, new ExponentialBackoffTcpReconnectionPolicy()));

//log an event
traceSource.TraceEvent(TraceEventType.Information, 1, "Test event");

```


#### Add logging to Splunk via a SLAB event sink
Below is a snippet showing how to create an `ObservableEventListener` and then
subscribe to events with a `UdpEventSink` (or `TcpEventSink`) configured 
to talk to localhost on port 10000. Next a `SimpleEventSource` is 
instantiated and a test event is generated.

```csharp
//setup
var listener = new ObservableEventListener();
listener.Subscribe(new UdpEventSink(IPAddress.Loopback, 10000));
// or, for TCP:
// listener.Subscribe(new TcpEventSink(IPAddress.Loopback, 10000, new ExponentialBackoffReconnectionPolicy()));

var eventSource = new SimpleEventSource();
listener.EnableEvents(eventSource, EventLevel.LogAlways, Keywords.All);

//log an event
eventSource.Message("Test event");

[EventSource(Name = "TestEventSource")]
public class SimpleEventSource : EventSource
{
    public class Keywords { }
    public class Tasks { }

    [Event(1, Message = "{0}", Level = EventLevel.Error)]
    internal void Message(string message)
    {
        this.WriteEvent(1, message);
    }
}
```

In both the example above, the TCP listeners took an extra argument, which specifies
how they should handle dropped TCP sessions. You can specify a custom reconnection
policy by defining an implementation of `Splunk.Logging.ITcpReconnectionPolicy` and passing it 
to the constructors of the `TcpTraceListener` or `TcpEventSink` classes. If you have
no particular policy in mind, use the ExponentialBackoffReconnectionPolicy provided by
the library, which retries after increasingly long intervals, starting from a delay of 
one second and going to a plateau of ten minutes.

`TcpConnectionPolicy` has a single method, Connect, which tries to establish a
connection or throws a `TcpReconnectFailure` if it cannot do so acceptably. Here is
annotated source code of the default, exponential backoff policy:

```csharp
public class ExponentialBackoffTcpReconnectionPolicy : ITcpReconnectionPolicy
{
    private int ceiling = 10 * 60; // 10 minutes in seconds

	// The arguments are:
	//
	//     connect - a function that attempts a TCP connection given a host, port number.
	//     host - the host to connect to
	//     port - the port to connect on
	//     cancellationToken - used by TcpTraceListener and TcpEventSink to cancel this method
	//         when they are disposed.
    public Socket Connect(Func<IPAddress, int, Socket> connect, IPAddress host, int port, CancellationToken cancellationToken)
    {
        int delay = 1; // in seconds
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                return connect(host, port);
            }
            catch (SocketException) { }

            // If this is cancelled via the cancellationToken instead of
            // completing its delay, the next while-loop test will fail,
            // the loop will terminate, and the method will return null
            // with no additional connection attempts.
            Task.Delay(delay * 1000, cancellationToken).Wait();
            // The nth delay is min(10 minutes, 2^n - 1 seconds).
            delay = Math.Min((delay + 1) * 2 - 1, ceiling);
        }

        // cancellationToken has been cancelled.
        return null;
    }
}
```

Another, simpler policy, would be trying to reconnect once, and then failing:

```csharp
class TryOnceTcpConnectionPolicy : ITcpReconnectionPolicy
{
    public Socket Connect(Func<System.Net.IPAddress, int, Socket> connect, 
            System.Net.IPAddress host, int port, 
            System.Threading.CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
                return null;
            return connect(host, port);
        }
        catch (SocketException e)
        {
            throw new TcpReconnectFailureException("Reconnect failed: " + e.Message);
        }
    }
}
```

### Handling errors from the TCP logging system

It can be difficult to diagnose connection problems in TCP logging without seeing
the exceptions that are actually thrown. The exceptions thrown during connection
attempts and by the reconnection policy are available by adding a handler to
`TcpEventSink` or `TcpTraceListener`.

Both `TcpEventSink` and `TcpTraceListener` have a method that takes an action
to be executed on each exception thrown in the logging system:

```csharp
public void AddLoggingFailureHandler(Action<Exception> handler)
```

For example, to write them to a local console, you would write:

```csharp
TcpTraceListener listener = ...;
listener.AddLoggingFailureHandler((ex) => {
    Console.WriteLine("{0}", ex);
});
```

### Sending events to HTTP Event Collector

This feature requires Splunk 6.3.0 and later.

After enabling [HTTP Event Collector](http://dev.splunk.com/view/event-collector/SP-CAAAE6M)
and creating an application token sending events is very simple:

```csharp
// TraceListener
var trace = new TraceSource("demo-logger");
trace.Listeners.Add(new HttpEventCollectorTraceListener(
    uri: new Uri("https://splunk-server:8088"),
    token: "<token-guid>"));
trace.TraceEvent(TraceEventType.Information, 0, "hello world");

// SLAB
var listener = new ObservableEventListener();
var sink = new HttpEventCollectorEventSink(
    uri: new Uri("https://splunk-server:8088"), 
    token: "token-guid",
    formatter: new AppEventFormatter());
listener.Subscribe(sink);
var eventSource = new AppEventSource();
listener.EnableEvents(eventSource, EventLevel.LogAlways, Keywords.All);
eventSource.Message("hello world");
```

#### Error Handling

A user application code can register an error handler that is invoked when 
HTTP Event Collector isn't able to send data.

```csharp
listener.AddLoggingFailureHandler((sender, HttpEventCollectorException e) =>
{
    // handle the error
});
```

### Changelog

The `CHANGELOG.md` file in the root of the repository contains a description
of changes for each version of Splunk logging for .NET. You can also
find it online at 

    https://github.com/splunk/splunk-library-dotnetlogging/blob/master/CHANGELOG.md

### Branches

The `master` branch always represents a stable and released version of the
Splunk logging for .NET. You can read more about our branching model
on our Wiki at 

    https://github.com/splunk/splunk-sdk-python/wiki/Branching-Model

## Documentation and resources

If you need to know more:

* For all things developer with Splunk, your main resource is the [Splunk Developer Portal](http://dev.splunk.com).
* For more about the Splunk REST API, see the [REST API Reference](http://docs.splunk.com/Documentation/Splunk/latest/RESTAPI).
* For more about about Splunk in general, see [Splunk>Docs](http://docs.splunk.com/Documentation/Splunk).

## Community

Stay connected with other developers building on Splunk.

<table>

<tr>
<td><em>Email</em></td>
<td><a href="mailto:devinfo@splunk.com">devinfo@splunk.com</a></td>
</tr>

<tr>
<td><em>Issues</em>
<td><a href="https://github.com/splunk/splunk-library-dotnetlogging/issues/">
https://github.com/splunk/splunk-library-dotnetlogging</a></td>
</tr>

<tr>
<td><em>Answers</em>
<td><a href="http://splunk-base.splunk.com/tags/csharp/">
http://splunk-base.splunk.com/tags/csharp/</a></td>
</tr>

<tr>
<td><em>Blog</em>
<td><a href="http://blogs.splunk.com/dev/">http://blogs.splunk.com/dev/</a></td>
</tr>

<tr>
<td><em>Twitter</em>
<td><a href="http://twitter.com/splunkdev">@splunkdev</a></td>
</tr>

</table>


### Contributions

If you want to make a code contribution, go to the 
[Open Source](http://dev.splunk.com/view/opensource/SP-CAAAEDM)
page for more information.

### Support

The Splunk logging library for .NET is community-supported.

1. You can find help through our community on [Splunk Answers](http://answers.splunk.com/) (use the `logging-library-dotnet` tag to identify your questions).
2. File issues on [GitHub](https://github.com/splunk/splunk-library-dotnetlogging/issues).

### Contact us

You can reach the Dev Platform team at [devinfo@splunk.com](mailto:devinfo@splunk.com).

## License

Splunk logging for .NET is licensed under the Apache License 2.0. Details can be found in the LICENSE file.
