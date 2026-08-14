# FluNET 0.9 Declarative Reconciliation freeze readiness

**Status: IMPLEMENTED SOURCE CANDIDATE — NOT VERIFIED / NOT FROZEN.**

Public `StandardLanguageIdentity.Version` remains `0.3`. This ledger records source implementation only; it is not a release claim.

## Implemented batches

- **66 — Desired/Observed State IR**: stable resource identity, keyed records, canonical JSON fingerprints and duplicate-key rejection.
- **67 — resource observation**: `IResourceObserverRegistry` plus built-in file, HTTP, SQL, environment and secret observers. Secret state contains a fingerprint rather than plaintext.
- **68 — SYNC compiler**: `SYNC target WITH source BY key`, explicit source-of-truth direction, inferred resource descriptors and a side-effect-free two-read analysis graph.
- **69 — keyed diff IR**: Create / Update / Delete / Unchanged plus optional three-way Conflict classification.
- **70 — reconciliation planner/runner**: concurrent target/source observation, diff calculation, conflict gate, no-op detection and ordinary SAVE mutation-plan synthesis for built-in local JSON targets.
- **71 — WATCH reconciliation bridge**: host-driven WATCH/WHEN signal routing to compiled SYNC definitions and the same reconciliation runner.
- **72 — compensation contracts**: explicit `COMPENSATE`; built-in inverse currently supports literal local SAVE only, and non-reversible effects are rejected.
- **73 — saga execution**: multiple ordinary execution-plan units share one compensation journal and restore/delete successful reversible effects in reverse order when a later unit fails.
- **74 — audit/history API**: run summaries and redacted audit events over the existing workflow journal, plus a durable single-host run catalog.
- **75 — stabilization, documentation and this freeze ledger**.

## SYNC semantics

```text
SYNC target WITH source BY id
```

means **source -> target**. The right side is desired/source-of-truth; the left side is observed/mutated target.

`SyncCompiler` is side-effect free. It stores an analysis-only read compilation with independent GET nodes. Runtime reconciliation does not execute that read graph to acquire state; `ReconciliationRunner` calls `IResourceObserverRegistry`, so local JSON arrays and custom observers are not constrained by the compatibility `LOAD CONFIG` representation.

## Observation contract

Built-in 0.9 observation boundaries:

| Resource | Observation |
| --- | --- |
| local JSON/CSV/XML/text | file capability + resource decoder registry |
| HTTP JSON/CSV/XML/text | HTTP capability + response metadata + decoder registry |
| SQL | `ISqlQueryExecutor` rows -> JSON records |
| environment | `{ name, value }` record |
| secret | `{ name, fingerprint }` record after secret policy check |

Custom `IResourceObserver` registrations are resolved before built-ins. Binary/Image are intentionally not treated as keyed reconciliation record sets.

## Diff and baseline rules

Without a baseline, desired vs observed produces Create/Update/Delete/Unchanged. With a baseline:

- same desired and observed -> Unchanged;
- only target drifted -> desired remains authoritative and produces the appropriate mutation;
- only desired changed -> desired mutation is applied;
- both sides changed the same key differently -> Conflict;
- two different independent creations under the same key -> Conflict.

Baseline selection/persistence is host-owned in 0.9; `ExecuteSyncAsync` uses two-way comparison unless a host calls `ReconciliationRunner.RunAsync` with a baseline snapshot.

## Mutation contract

The built-in mutation synthesizer owns one concrete target contract: a single local JSON file. It serializes the desired keyed snapshot deterministically, registers it as a typed `Text` value, compiles a normal compact SAVE and delegates execution to `ExecutionPlanExecutor`.

Other target kinds are rejected with `ReconciliationMutationNotSupportedException` until a future explicit mutation-provider contract exists. Conflicts never produce a mutation plan.

## WATCH, compensation and saga safety

Reconciliation WATCH is host-driven: no hidden polling thread is created. Signals route to compiled SYNC definitions.

Compensation is explicit and inverse-driven. Built-in `COMPENSATE` supports literal local SAVE only. It is not ACID and does not pretend that email/generic POST can be undone. Saga execution composes ordinary plans and replays successful reversible effects in reverse order after a later failure.

## Audit/history

`WorkflowHistoryService` projects the existing journal into run summaries and redacted audit events. Raw `ResultJson` is not returned by the public audit event; only presence + SHA-256 hash are exposed. `DurableWorkflowRunCatalog` discovers checksummed durable journals in one host directory.

## 0.9 contract tests added to source

Source tests cover, among other things:

- canonical state fingerprints and duplicate keys;
- local JSON-array observation;
- SYNC source-to-target parsing and independent read analysis;
- quoted SQL keyword protection;
- two-way and three-way diff semantics;
- local JSON reconciliation apply/no-op/conflict behavior;
- WATCH signal -> real SYNC execution;
- rejection of non-reversible compensation;
- restoration of pre-existing SAVE content after later failure;
- saga delete/restore compensation across units;
- redacted history and durable run discovery.

These tests exist in source but have **not** been executed in this session because a usable .NET SDK/build environment is not available here.

## Architectural invariant

Observation, diffing, WATCH routing, compensation and saga coordination are orchestration layers. Command mutation still goes through the existing typed `ExecutionPlanExecutor`; no reconciliation-specific command executor exists.

## Release gate

Before any 0.9 version/tag/freeze claim, the exact candidate must pass:

```bash
dotnet restore FluNET.sln
dotnet build FluNET.sln --configuration Release --no-restore
dotnet test FluNET.sln --configuration Release --no-build
```

Until that evidence exists, 0.9 remains **IMPLEMENTED SOURCE CANDIDATE, NOT VERIFIED / NOT FROZEN**.
