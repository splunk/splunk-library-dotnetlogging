# The Splunk Library for .NET Logging
## Version 1.0 beta

The Splunk Library for .NET Logging enables developers
to easily log to Splunk via TraceListeners or the Semantic Logging Application
Block from Microsoft.

In particular, it provides:

* A `UdpTraceListener` which is a [.NET Trace Listener] (http://msdn.microsoft.com/en-us/library/4y5y10s7(v=vs.110).aspx) that logs events to Splunk over UDP. Popular logging frameworks support appending to the Trace Infrastructure including [Log4Net](http://logging.apache.org/log4net/release/sdk/log4net.Appender.TraceAppender.html), NLog (http://nlog-project.org/documentation/v2.0.1/html/T_NLog_Targets_TraceTarget.htm)  and [Enterprise Library] (http://msdn.microsoft.com/en-us/library/dn440731(v=pandp.60).aspx). 
* A `UdpEventSink` which is a Semantic Logging Application Block [Sink] (http://msdn.microsoft.com/en-us/library/dn440729(v=pandp.60).aspx#sec29) that logs ETW events to Splunk over UDP.

## Supported platforms

.NET 4.5 / Windows 8.1

## Advice

### Splunk Universal Forwarder vs Splunk UDP Inputs

If you can, it is better to log to files and monitor them with a Splunk 
Universal Forwarder. This provides you with the features of the Universal 
Forwarder, and added robustness from having persistent files. However, there 
are situations where using a Universal Forwarder is not a possibility. In 
these cases, writing directly to a UDP input is a reasonable approach.

### Data Cloning

You can use [data cloning](http://docs.splunk.com/Splexicon:Datacloning) by 
providing multiple instances of your UDP handler in your logging 
configuration, each instance pointing to different indexers.

### Load Balancing

Rather than trying to reinvent 
[load balancing](http://docs.splunk.com/Splexicon:Loadbalancing) across your 
indexers in your log configuration, set up a Splunk Universal Forwarder with a 
UDP input. Have all your logging sources write to that UDP input, and use the 
Universal Forwarder's load balancing features to distribute the data from 
there to a set of indexers.

## Getting started with the Splunk Library for .NET Logging

The Splunk SDK for C# contains a library for logging events to a Splunk Enterprise instance from within your .NET applications.  

The information in this Readme provides steps to get going quickly. In the 
future we plan to roll out more in-depth documentation.

### Requirements

Here's what you need to get going with the Splunk Library for .NET Logging.

#### Splunk Enterprise

If you haven't already installed Splunk Enterprise, download it at 
<http://www.splunk.com/download>. For more information about installing and 
running Splunk Enterprise and system requirements, see the
[Splunk Installation Manual](http://docs.splunk.com/Documentation/Splunk/latest/Installation). 

#### Developer environments

The Splunk SDK for C# supports development in the following environments:

##### Visual Studio
The Splunk SDK for C# supports development in [Microsoft Visual Studio](http://www.microsoft.com/visualstudio/downloads) 2012 and later

You will need to install [Code Contracts for .NET](http://visualstudiogallery.msdn.microsoft.com/1ec7db13-3363-46c9-851f-1ce455f66970)
(be sure to close Visual Studio before you install it or the install will not work, despite appearing to) 

To run the unit tests you will need to install an [xUnit](https://github.com/xunit/xunit) runner:
* If you use resharper, install its [xUnit.net Test Support](https://resharper-plugins.jetbrains.com/packages/xunitcontrib/1.6.2).
* Otherwise, install the [xUnit.net runner for Visual Studio 2012 and 2013](http://visualstudiogallery.msdn.microsoft.com/463c5987-f82b-46c8-a97e-b1cde42b9099).

### Splunk Library for .NET Logging

#### MyGet feed

Before the intial release, you can download the Splunk SDK C# NuGet packages from [MyGet](http://www.myget.org). Add the following feed to your package sources in Visual Studio: https://splunk.myget.org/F/splunk-library-dotnetlogging. The feed contains the Splunk.Logging package.

*Note*: This will be published to NuGet when the SDK releases.

#### Getting the source

[Get the Splunk Logging library for .NET](https://github.com/splunk/splunk-library-dotnetlogging). Download the ZIP file and extract its contents.

If you are interested in contributing to the Splunk Logging Library for .NET, you can 
[get it from GitHub](https://github.com/splunk/splunk-library-dotnetlogging) and clone the 
resources to your computer.

#### Building the SDK

To build from source after extracting or cloning the SDK, do the following"

1. At the root level of the **splunk-library-dotnetlogging** directory, open the 
**splunk-library-dotnetlogging.sln** file in Visual Studio.
2. On the **BUILD** menu, click **Build Solution**.

This will build the SDK, the examples, and the unit tests.

#### Solution Layout

The solution is organized into `src` and `test` folders. `src` contains a single
Visual Studio project `Splunk.Logging`. `test` contains a single project, 
`unit-tests`.

#### Examples and unit tests

The Splunk Logging Library for .NET includes full unit tests which run using [xunit](https://github.com/xunit/xunit) as well as several examples.

### Adding logging to Splunk via a TraceListener
Below is a snippet showing creating a `TraceSource` and then attaching a UpdTraceListener configured to talk to localhost on port 10000. Next an event is generated which is sent to Splunk.
```csharp
//setup
var traceSource = new TraceSource("TestLogger");
traceSource.Listeners.Remove("Default");
traceSource.Switch.Level = SourceLevels.All;
traceSource.Listeners.Add(new UdpTraceListener(IPAddress.Loopback, 10000));
// or, for TCP:
// traceSource.Listeners.Add(new TcpTraceListener(IPAddress.Loopback, 10000));

//log an event
traceSource.TraceEvent(TraceEventType.Information, 1, "Test event");

```


### Adding logging to Splunk via a SLAB event sink
Below is a snippet showing creating an `ObservableEventListener` and then subscribing to events with a UdpEventSink configured to talk to localhost on port 1000. Next a SimpleEventSource is instantiated and a test event is generated.

```csharp
//setup
var listener = new ObservableEventListener();
listener.Subscribe(new UdpEventSink(IPAddress.Loopback, 10000));
// or, for TCP:
// listener.Subscribe(new TcpEventSink(IPAddress.Loopback, 10000));

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

### Customizing TCP session handling

By default, TCP listeners handle dropped TCP sessions by trying to reconnect
after increasingly long intervals. You can specify a custom reconnection policy
by defining an instance of Splunk.Logging.TcpConnectionPolicy, and passing it to
the constructors of the TcpTraceListener and TcpEventSink classes.

TcpConnectionPolicy has a single method, Reconnect, which tries to establish a
connection or throws a TcpReconnectFailure if it cannot do so acceptably. Here is
annotated source code of the default, exponential backoff policy:

```
public class ExponentialBackoffTcpReconnectionPolicy : TcpReconnectionPolicy
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

```
public class SingleRetryTcpConnectionPolicy
    {
        public Socket Reconnect(Func<Socket> connect, CancellationToken cancellationToken)
        {
            try
            {
                return connect();
            }
            catch (SocketException e)
            {
                throw new TcpReconnectFailure("Failed to reconnect: " + e.Message);
            }
        }
    }
```

### Changelog

The **CHANGELOG.md** file in the root of the repository contains a description
of changes for each version of the Splunk Library for .NET Logging. You can also
find it online at 

    (TODO: Link to github)

### Branches

The **master** branch always represents a stable and released version of the
Splunk Library for .NET Logging. You can read more about our branching model
on our Wiki at 

    https://github.com/splunk/splunk-sdk-python/wiki/Branching-Model

## Documentation and resources

If you need to know more:

* For all things developer with Splunk, your main resource is the [Splunk
  Developer Portal](http://dev.splunk.com).

* For more about the Splunk REST API, see the [REST API 
  Reference](http://docs.splunk.com/Documentation/Splunk/latest/RESTAPI).

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

This product is currently in development and officially unsupported. We will be triaging any issues filed by the community however and addressing them as appropriate. Please [file](https://github.com/splunk/splunk-sdk-csharp-pcl) issues for any problems that you encounter.

### Contact Us

You can reach the Dev Platform team at devinfo@splunk.com.

## License

The Splunk Logging Library for .NET is licensed under the Apache License 2.0. Details can be 
found in the LICENSE file.
