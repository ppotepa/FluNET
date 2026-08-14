# FluNET 0.9 Declarative Reconciliation freeze readiness

**Status: IMPLEMENTED SOURCE CANDIDATE — NOT VERIFIED / NOT FROZEN.**

Public `StandardLanguageIdentity.Version` remains `0.3`. This ledger records source implementation only; it is not a release claim.

## Implemented batches

- **66 — Desired/Observed State IR**: stable resource identity, keyed records, canonical JSON fingerprints and duplicate-key rejection.
- **67 — resource observation**: pluggable observer registry with built-in file, HTTP, environment, secret and SQL observation boundaries.
- **68 — SYNC compiler**: `SYNC target WITH source BY key` with explicit source-of-truth direction and independent observation reads.
- **69 — keyed diff IR**: Create / Update / Delete / Unchanged plus optional three-way Conflict classification.
- **70 — reconciliation planner/runner**: ordinary read plans, diff calculation and ordinary SAVE mutation-plan synthesis for built-in local JSON targets.
- **71 — WATCH reconciliation bridge**: host-driven WATCH/WHEN signal routing to compiled SYNC definitions and the same reconciliation runner.
- **72 — compensation contracts**: explicit `COMPENSATE` opt-in; built-in inverse currently supports literal local SAVE only. Unknown/non-reversible effects are rejected instead of receiving pretend rollback semantics.
- **73 — saga execution**: multiple ordinary execution-plan units share one compensation journal and reverse successful reversible effects when a later unit fails.
- **74 — audit/history API**: run summaries and redacted audit events over the existing workflow journal, with an optional durable run catalog.
- **75 — this freeze ledger**.

## Reconciliation semantics

```text
SYNC target WITH source BY id
```

means **source -> target**. The right side is desired/source-of-truth; the left side is observed/mutated target.

The built-in mutation synthesizer currently owns concrete local JSON file targets. Other targets need a custom mutation provider rather than implicit hidden side effects.

## Compensation safety

Compensation is explicit, capability-aware and inverse-driven. Built-in `COMPENSATE` deliberately supports only operations for which the runtime has a deterministic inverse. It does not imply ACID transactions and it does not claim that arbitrary external effects such as email or generic POST can be undone.

## Architectural invariant

Observation, diffing and saga coordination are orchestration layers. Command execution still goes through the existing typed `ExecutionPlanExecutor`; no reconciliation-specific command executor exists.

## Release gate

Before any 0.9 version/tag/freeze claim, the exact candidate must pass:

```bash
dotnet restore FluNET.sln
dotnet build FluNET.sln --configuration Release --no-restore
dotnet test FluNET.sln --configuration Release --no-build
```

Until that evidence exists, 0.9 remains **IMPLEMENTED, NOT VERIFIED**.
