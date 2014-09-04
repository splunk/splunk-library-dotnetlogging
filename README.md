# The Splunk Logging Libraries for .NET 
#### Version 1.0

The Splunk logging libraries for .NET enable you to configure UDP or TCP logging of events to a Splunk Enterprise instance from within your .NET applications, via a .NET TraceListener or a Semantic Logging Application Block (SLAB) event sink.

Each library consists of several extensions for existing .NET logging frameworks. Specifically, there are two libraries available, along with a third common library that is required by both main libraries:

* **Splunk.Logging.TraceListener** contains [.NET trace listeners](http://msdn.microsoft.com/library/4y5y10s7.aspx) that log events to Splunk Enterprise over UDP or TCP. Popular logging frameworks support appending to the trace infrastructure, including [Log4Net](http://logging.apache.org/log4net/release/sdk/log4net.Appender.TraceAppender.html), [NLog](http://nlog-project.org/documentation/v2.0.1/html/T_NLog_Targets_TraceTarget.htm), and [Enterprise Library](http://msdn.microsoft.com/library/dn440731.aspx). .NET trace listeners are cross-platform. The library defines the following trace listeners:
    * **UdpTraceListener**
    * **TcpTraceListener**
* **Splunk.Logging.SLAB** contains event sinks, which are [Semantic Logging Application Block (SLAB) sinks](http://msdn.microsoft.com/library/dn440729.aspx#sec29) that log Event Tracing for Windows (ETW) events to Splunk Enterprise over UDP or TCP. SLAB sinks are for Windows only. The library defines the following sinks:
    * **UdpEventSink**
    * **TcpEventSink**
* **Splunk.Logging.Common** is a common library that contains resources required by both logging libraries. You must include the **Splunk.Logging.Common** library when you're using either of the other two libraries. The common library defines:
    * Wrappers around a .NET [**Socket**](http://msdn.microsoft.com/en-us/library/system.net.sockets.socket.aspx) object (for UDP and TCP).
    * Policy for how TCP logging should behave (reconnect intervals, when to throw an exception) when there is a socket error.
    * A TCP socket writer that maintains a queue of strings to be sent.

## Advice

### Splunk Universal Forwarder vs. Splunk UDP Inputs

If you can, it is better to log to files and monitor them with a [Splunk 
Universal Forwarder](http://www.splunk.com/download/universalforwarder). This provides you with the features of the Universal 
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

You have several options for installing the Splunk logging libraries for .NET. For more information, see [Install the Splunk logging libraries for .NET](http://dev.splunk.com/view/splunk-loglib-dotnet/SP-CAAAEYC)


#### Solution Layout

The solution is organized into `src` and `test` folders. `src` contains a single
Visual Studio project `Splunk.Logging`. `test` contains a single project, 
`unit-tests`.

#### Examples and unit tests

The Splunk logging libraries for .NET include full unit tests which run using [xunit](https://github.com/xunit/xunit) as well as several examples.

### How to use the Splunk logging libraries for .NET
This topic includes examples of how to use both the .NET trace listener and SLAB sink libraries.
* [Add logging using a .NET trace listener](http://dev.splunk.com/view/splunk-loglib-dotnet/SP-CAAAEX9)
* [Add logging using a SLAB sink](http://dev.splunk.com/view/splunk-loglib-dotnet/SP-CAAAEYA)
* [Customize TCP session handling](http://dev.splunk.com/view/splunk-loglib-dotnet/SP-CAAAEY9)

### Changelog

The **CHANGELOG.md** file in the root of the repository contains a description
of changes for each version of the Splunk logging libraries for .NET. You can also
find it online at 

    https://github.com/splunk/splunk-library-dotnetlogging/blob/master/CHANGELOG.md

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

The Splunk Logging Libraries for .NET are licensed under the Apache License 2.0. Details can be found in the LICENSE file.
