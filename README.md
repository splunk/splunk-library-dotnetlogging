# The Splunk Library for .NET Logging

#### Version 1.0

The Splunk Library for .NET Logging contains library code to enable developers
to easily log to Splunk via TraceListeners or the Semantic Logging Application
Block from Microsoft.

Splunk is a search engine and analytic environment that uses a distributed
map-reduce architecture to efficiently index, search and process large 
time-varying data sets.

The Splunk product is popular with system administrators for aggregation and
monitoring of IT machine data, security, compliance and a wide variety of 
other scenarios that share a requirement to efficiently index, search, analyze
and generate real-time notifications from large volumes of time series data.

The Splunk developer platform enables developers to take advantage of the 
same technology used by the Splunk product to build exciting new applications
that are enabled by Splunk's unique capabilities.

## Getting started with the Splunk Library for .NET Logging

This section will get you up to speed using the Splunk Library for .NET Logging.

### Requirements

Here's what you need to get going with the Splunk Library for .NET Logging.

#### Splunk

If you haven't already installed Splunk, download it 
[here](http://www.splunk.com/download). For more about installing and running 
Splunk and system requirements, see 
[Installing & Running Splunk](http://dev.splunk.com/view/SP-CAAADRV). 

### Splunk Library for .NET Logging

The preferred way to get the Splunk Library for .NET Logging is via NuGet. In
Visual Studio, 

1. Install the 'NuGet Package Manager for Visual Studio 2013' (or the
   appropriate version of Visual Studio) via the Tools->Extensions and Updates
   menu.
2. In your project, right click on 'References' in your project and click
   'Manage NuGet Packages'. Search for 'Splunk.Logging', and install it.

You can also get the source from GitHub at

    https://github.com/splunk/splunk-library-dotnetlogging

The repository is organized as a Visual Studio solution. The easiest way to
build it is to open the solution in Visual Studio and run 'Build solution'
from the Build menu.

### Adding logging to Splunk via a TraceListener


### Adding logging to Splunk via a SLAB event sink



## Building the library

The Splunk Library for .NET Logging is organized as a Visual Studio solution.
To build it, open the solution in Visual Studio and run 'Build solution' from
the Build menu. This will generate a dll containing both listeners.

## Run the unit tests

The Splunk Library for .NET Logging has unit tests which depend on xUnit. To
run them:

1. Install the 'xUnit.net runner for Visual Studio 2012 and 2013' from the
   Tools->Extensions and Updates menu in Visual Studio.
2. Build the project.
3. Open the Test Explorer via the Test->Windows->Test Explorer menu item.

The tests should be listed in the Test Explorer. Click 'Run All' to run them.
They should require no additional configuration.

## Repository

The solution is organized into `src` and `test` folders. `src` contains a single
Visual Studio project `Splunk.Logging`. `test` contains a single project, 
`unit-tests`.

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

* For all things developer with Splunk, your main resource is the [Splunk Developer Portal](http://dev.splunk.com).

* For more about the Splunk REST API, see the [REST API Reference](http://docs.splunk.com/Documentation/Splunk/latest/RESTAPI).

* For more about about Splunk in general, see [Splunk>Docs](http://docs.splunk.com/Documentation/Splunk).

## Community

Stay connected with other developers building on Splunk.

<table>

<tr>
<td><b>Email</b></td>
<td>devinfo@splunk.com</td>
</tr>

<tr>
<td><b>Issues</b>
<td><span>https://github.com/splunk/splunk-library-dotnetlogging/issues/</span></td>
</tr>

<tr>
<td><b>Blog</b>
<td><span>http://blogs.splunk.com/dev/</span></td>
</tr>

<tr>
<td><b>Twitter</b>
<td>@splunkdev</td>
</tr>

</table>


### How to contribute

If you would like to contribute to the library, go here for more information:

* [Splunk and open source](http://dev.splunk.com/view/opensource/SP-CAAAEDM)

* [Individual contributions](http://dev.splunk.com/goto/individualcontributions)

* [Company contributions](http://dev.splunk.com/view/companycontributions/SP-CAAAEDR)

### Support

1. You will be granted support if you or your company are already covered 
   under an existing maintenance/support agreement. Send an email to 
   _support@splunk.com_ and include "Splunk Library for .NET Logging" in
   the subject line. 

2. If you are not covered under an existing maintenance/support agreement, you 
   can find help through the broader community at:

   <ul>
   <li><a href='http://splunk-base.splunk.com/answers/'>Splunk Answers</a> (use
    the (TODO: What tags?) tags to identify your questions)</li>
   <li><a href='http://groups.google.com/group/splunkdev'>Splunkdev Google 
   Group</a></li>
   </ul>
3. Splunk will NOT provide support for the library if it has been modified. If
   you modify a library and want support, you can find help through the broader
   community and Splunk answers (see above). We would also like to know why you
   modified the core library&mdash;please send feedback to _devinfo@splunk.com_.
4. File any issues on [GitHub](https://github.com/splunk/splunk-library-dotnetlogging/issues).


### Contact Us

You can reach the Developer Platform team at _devinfo@splunk.com_.

## License

The Splunk Library for .NET Logging is licensed under the Apache
License 2.0. Details can be found in the LICENSE file.
