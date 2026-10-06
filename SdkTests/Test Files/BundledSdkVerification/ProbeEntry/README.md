# ProbeEntry

**Project Type**: C# Library

**Size**: XS

## Summary

This **multitarget library** targets netstandard2.0 and net48 and delegates to ProbeBase. Debug and Release use deliberately different assembly versions so package inspection can detect the wrong build output.

## Library Info

**Assembly Name**: ProbeEntry

**Consumers**: HarvestingScript and HarvestingGqi.

**Public API**: `Harvesting.Probe.Entry.GetValue()` returns ProbeBase's fixed string.

See the [solution verification](../README.md) for expected TFM, version, and byte checks.
