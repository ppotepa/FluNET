# .NET API 1.0 candidate boundary

This document records the intended direction of the public .NET hosting API before FluNET reaches a stable 1.0 contract. It does not remove any existing entry point.

## Preferred hosting surface

New embedding code should use explicit context ownership and asynchronous execution:

- `FluNETContext.Create()` or `FluNETContext.CreateWithRuntime(...)`, disposed by the host;
- `Engine.Analyze(...)` for effect-free compilation and planning;
- `Engine.ExecuteAsync(...)` for execution and workflow resume scenarios;
- the surface compilation/execution extension APIs for compact `.flu` / `.fln` source.

These APIs make lifetime, cancellation and execution behavior visible to the host and are the candidates for the stable 1.0 direction.

## Pre-1.0 compatibility surface

The following APIs remain available for existing callers but should not be the basis of new integrations:

- `FluNETContext.Default`, because it introduces process-global mutable lifetime;
- `Engine.Execute(...)`, because it synchronously blocks the asynchronous execution pipeline;
- `Engine.Run(...)`, because it returns a legacy tuple whose `SourceSentence` member is always `null`.

These members are marked obsolete to make migration visible during development while preserving source/binary compatibility for the current preview line.

## Version identities

The engine package and command-line tool packages intentionally have separate version identities. Their shared definitions live in `Directory.Build.props` so package projects do not silently drift from one another.

## 1.0 rule

Before declaring a stable 1.0 API, review obsolete compatibility members, public records/interfaces, host security defaults and package-version policy together. Any removal or signature change should be made deliberately as part of the 1.0 compatibility decision, not as incidental refactoring.
