# FluNET master roadmap

Status vocabulary: **IMPLEMENTED** means the source contract exists on `main`; **VERIFIED** requires a successful Release restore/build/test against the exact tree; **FROZEN/RELEASED** additionally requires an explicit version promotion. Public `StandardLanguageIdentity.Version` remains `0.3` until that gate is satisfied.

## 0.8 — Integration & Execution

Batches **52–65: IMPLEMENTED source candidate / NOT VERIFIED**.

52 roadmap · 53 resource payload/decoders · 54 CSV/XML · 55 Binary/Image · 56 typed HTTP media · 57 SQL · 58 AUTH · 59 nested action IR · 60 full FOR EACH actions · 61 policy v2 · 62 durable cache/idempotency · 63 calendar/cron · 64 automation/ENSURE CLI adapters · 65 tooling/freeze ledger.

See [0.8 freeze readiness](compiler-0.8-freeze-readiness.md).

## 0.9 — Declarative Reconciliation

Batches **66–75: IMPLEMENTED source candidate / NOT VERIFIED**.

| Batch | Scope | Status |
| --- | --- | --- |
| 66 | Desired/Observed State IR + canonical fingerprints | IMPLEMENTED |
| 67 | pluggable resource observation | IMPLEMENTED |
| 68 | `SYNC target WITH source BY key` compiler | IMPLEMENTED |
| 69 | keyed Create/Update/Delete/Unchanged/Conflict diff IR | IMPLEMENTED |
| 70 | reconciliation mutation planner / runner | IMPLEMENTED |
| 71 | WATCH / WHEN reconciliation signal bridge | IMPLEMENTED |
| 72 | explicit compensation contracts | IMPLEMENTED |
| 73 | saga execution | IMPLEMENTED |
| 74 | redacted audit/history API + durable run catalog | IMPLEMENTED |
| 75 | 0.9 freeze/stabilization ledger | IMPLEMENTED / NOT VERIFIED |

The repaired 0.9 path is now concrete rather than roadmap-only:

```text
SYNC source
  -> SyncCompiler
  -> SyncDefinition
  -> IResourceObserverRegistry
  -> Desired/Observed snapshots
  -> ReconciliationDiffEngine
  -> ReconciliationMutationPlanner
  -> ordinary SurfaceCompiler / ExecutionPlan
  -> ExecutionPlanExecutor
```

See [0.9 freeze readiness](compiler-0.9-freeze-readiness.md).

## 1.0 — Stable Platform candidate

Separate 1.0 candidate material exists on `main`, but it is **outside the 0.9 freeze** and remains unverified. The 0.9 closure does not promote the public language version and does not make a 1.0 release claim.
