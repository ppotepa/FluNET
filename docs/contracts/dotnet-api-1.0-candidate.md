# .NET API 1.0 candidate boundary

This document records the public .NET hosting boundary being prepared for FluNET 1.0.
The preview-only global and synchronous compatibility entry points have now been removed from
the shipped engine assembly so the candidate surface has one clear lifetime and execution model.

## Preferred hosting surface

Embedding code should use explicit context ownership and asynchronous execution:

- `FluNETContext.Create()` or `FluNETContext.CreateWithRuntime(...)`;
- `using` for synchronous disposal or `await using` for asynchronous disposal;
- `Engine.Analyze(...)` for effect-free compilation and planning;
- `Engine.ExecuteAsync(...)` for execution and workflow resume scenarios;
- the surface compilation/execution extension APIs for compact `.flu` / `.fln` source.

These APIs make lifetime, cancellation and execution behavior visible to the host and form the
candidate stable 1.0 direction.

## Removed preview compatibility surface

The following preview members are intentionally no longer part of the public engine API:

- `FluNETContext.Default` and `FluNETContext.ResetDefault()`, which introduced process-global mutable lifetime;
- synchronous `Engine.Execute(...)`, which blocked the asynchronous execution pipeline;
- tuple-shaped `Engine.Run(...)`, whose `SourceSentence` member was permanently `null`.

Callers should create and own a context explicitly and consume `ExecutionResult` from
`Engine.ExecuteAsync(...)`. Behavioral tests for the historical syntax may use test-only adapters,
but those adapters are not compiled into or shipped with FluNET packages.

## Version identities

The engine package and command-line tool packages intentionally have separate version identities.
Their shared definitions live in `Directory.Build.props` so package projects do not silently drift
from one another.

## 1.0 rule

Before declaring a stable 1.0 API, review public records/interfaces, host security defaults and
package-version policy together. From this boundary forward, any public removal or signature
change should be made deliberately as a compatibility decision rather than incidental refactoring.
