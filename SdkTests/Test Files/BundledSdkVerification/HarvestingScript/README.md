# HarvestingScript

**Project Type**: Automation Script

**Size**: XS

## Summary

This **harvesting probe** compiles a call to ProbeEntry, which calls the transitive ProbeBase library. Its root selects build-only SDM 1.0.3 while the referenced graph carries Registration.Common 2.0.0.

## Purpose

Internal compilation and final-package verification. The entry point logs the library's fixed probe value; it performs no business operation. See the [solution commands and exact DLL checks](../README.md).

## Interactions

The script does not interact with DataMiner objects.

## Input Parameters

This script has no input parameters.
