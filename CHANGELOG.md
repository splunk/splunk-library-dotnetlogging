# Splunk logging for .NET Changelog

## Version 1.7.0

* Update version of Newtonsoft.JSON to 11.0.2 (GitHub pull request #34).
* Make HEC timestamp invariant to culture (GitHub pull request #20).

## Version 1.6.1

* Add support for overriding metadata with `HttpEventCollectorSender.Send()`.

## Version 1.6.0

* Add support for custom HTTP Event Collector formatter function for TraceListener.
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
