# The Splunk Logging Libraries for .NET 
#### Version 1.0

The Splunk logging libraries for .NET enable you to configure UDP or TCP logging of events to a Splunk Enterprise instance from within your .NET applications, via a .NET TraceListener or a Semantic Logging Application Block (SLAB) event sink.

Each library consists of several extensions for existing .NET logging frameworks. Specifically, there are two libraries available, along with a third common library that is required by both main libraries:

* **Splunk.Logging.TraceListener** 
* **Splunk.Logging.SLAB** 
* **Splunk.Logging.Common**

## Advice

### Splunk Universal Forwarder vs. Splunk UDP Inputs

If you can, it is better to log to files and monitor them with a [Splunk 
Universal Forwarder](http://www.splunk.com/download/universalforwarder). This provides you with the features of the Universal 
Forwarder, and added robustness from having persistent files. However, there 
are situations where using a Universal Forwarder is not a possibility. In 
these cases, writing directly to a UDP input is a reasonable approach.

### Data cloning

You can use [data cloning](http://docs.splunk.com/Splexicon:Datacloning) by 
providing multiple instances of your UDP handler in your logging 
configuration, each instance pointing to different indexers.

### Load balancing

Rather than trying to reinvent 
[load balancing](http://docs.splunk.com/Splexicon:Loadbalancing) across your 
indexers in your log configuration, set up a Splunk Universal Forwarder with a 
UDP input. Have all your logging sources write to that UDP input, and use the 
Universal Forwarder's load balancing features to distribute the data from 
there to a set of indexers.

## Get started 

The information in this Readme provides steps to get going quickly, but for more in-depth information be sure to visit the [Splunk logging libraries for .NET](http://dev.splunk.com/view/splunk-loglib-dotnet/SP-CAAAEX4) page on [Splunk Developer Portal](http://dev.splunk.com).

### Requirements

Here's what you need to get going with the Splunk logging libraries for .NET:

* **.NET Framework 4.0 or later**: The Splunk logging libraries for .NET require at least .NET 4.0.
* **xUnit runner**: If you use ReSharper, install its [xUnit.net Test Support](https://resharper-plugins.jetbrains.com/packages/xunitcontrib/). Otherwise, install the [xUnit.net runner for Visual Studio 2012 and 2013](http://visualstudiogallery.msdn.microsoft.com/463c5987-f82b-46c8-a97e-b1cde42b9099).
* **Splunk Enterprise**: If you haven't already installed Splunk Enterprise, download it at [http://www.splunk.com/download](http://www.splunk.com/download). For more information about installing and running  Splunk Enterprise and system requirements, see the [Splunk Installation Manual](http://docs.splunk.com/Documentation/Splunk/latest/Installation).
* **Visual Studio (optional)**: The Splunk logging libraries for .NET supports development in [Microsoft Visual Studio 2012 or later](http://www.microsoft.com/visualstudio/downloads).
* **Code Contracts for .NET (optional)**: If you are using Visual Studio, install [Code Contracts for .NET](http://visualstudiogallery.msdn.microsoft.com/1ec7db13-3363-46c9-851f-1ce455f66970). Be sure to close Visual Studio before installing the package.

### Install

You have several options for installing the Splunk logging libraries for .NET. The most common method is through NuGet. Add the package you want after searching for "splunk" in the Manage NuGet Packages window in Visual Studio.

For more information, and for information about other ways to install the Splunk logging libraries for .NET, see [Install the Splunk logging libraries for .NET](http://dev.splunk.com/view/splunk-loglib-dotnet/SP-CAAAEYC)


#### Solution layout

The solution is organized into **src** and **test** directories. The **src** directory contains three libraries: **Splunk.Logging.TraceListener** (which contains [.NET trace listeners](http://msdn.microsoft.com/library/4y5y10s7.aspx) that log events to Splunk Enterprise over UDP or TCP), **Splunk.Logging.SLAB** (which contains [Semantic Logging Application Block (SLAB) event sinks](http://msdn.microsoft.com/library/dn440729.aspx#sec29) that log ETW events to Splunk Enterprise over UDP or TCP), and **Splunk.Logging.Common** (a common library that contains resources required by both logging libraries). The **test** directory contains a single project, **unit-tests**.

#### Examples and unit tests

The Splunk logging libraries for .NET include full unit tests which run using [xunit](https://github.com/xunit/xunit) as well as several examples.

### Example code

#### Add logging to Splunk via a TraceListener
Below is a snippet showing creating a **TraceSource** and then attaching a **UdpTraceListener** (or **TcpTraceListener**) configured to talk to localhost on port 10000. Next an event is generated which is sent to Splunk.

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

#### Add logging to Splunk via a SLAB event sink
Below is a snippet showing how to create an **ObservableEventListener** and then subscribe to events with a **UdpEventSink** (or **TcpEventSink**) configured to talk to localhost on port 10000. Next a **SimpleEventSource** is instantiated and a test event is generated.

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

#### Customize TCP session handling
By default, the TCP listeners handle dropped TCP sessions by trying to reconnect after increasingly long intervals. You can specify a custom reconnection policy by defining an instance of **Splunk.Logging.TcpConnectionPolicy** and passing it to the constructors of the **TcpTraceListener** or **TcpEventSink** classes.

**TcpConnectionPolicy** has a single method, **Reconnect**, which tries to establish a connection or throws a **TcpReconnectFailure** if it cannot do so within a reasonable amount of time. 

For a code example, see [Customize TCP session handling](http://dev.splunk.com/view/splunk-loglib-dotnet/SP-CAAAEY9).

### Changelog

The **CHANGELOG.md** file in the root of the repository contains a description
of changes for each version of the Splunk logging libraries for .NET. You can also
find it online at 

    https://github.com/splunk/splunk-library-dotnetlogging/blob/master/CHANGELOG.md

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

This product is currently in development and officially unsupported. We will be triaging any issues filed by the community however and addressing them as appropriate. Please [file](https://github.com/splunk/splunk-sdk-csharp-pcl) issues for any problems that you encounter.

### Contact us

You can reach the Dev Platform team at devinfo@splunk.com.

## License

The Splunk Logging Libraries for .NET are licensed under the Apache License 2.0. Details can be found in the LICENSE file.
