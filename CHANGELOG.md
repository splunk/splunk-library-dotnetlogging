# Splunk Library for .NET Logging Changelog

## Version 1.6.0

* Add support for custom Event Collector formatter function for TraceListener.
* Add support for setting timestamp other than UtcNow (GitHub Pull request #15).

## Version 1.5.0

* Add support for HTTP Event Collector.

## Version 1.1

### Performance improvements

* `TcpSocketWriter` now uses a `BlockingCollection` instead of a `ConcurrentQueue` internally, resulting in significantly less CPU utilization.

### Minor changes

* Added xunit.runner as a dependency.

## Version 1.0

* Add support for logging via TCP.
* Fix behavior of TraceListeners. Now they write to the network on every invocation of Write or WriteLine
  and no longer try to insert timestamps.

## Version 0.8 (beta)

* Initial release.
