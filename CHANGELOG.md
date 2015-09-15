# Splunk Library for .NET Logging Changelog

## Version 1.5
* Add support for HTTP Event Collector.

## Version 1.0

* Add support for logging via TCP.
* Fix behavior of TraceListeners. Now they write to the network on every invocation of Write or WriteLine
  and no longer try to insert timestamps.

## Version 0.8 (beta)

* Initial release.
