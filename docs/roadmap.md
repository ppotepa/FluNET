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

See [0.9 freeze readiness](compiler-0.9-freeze-readiness.md).

## 1.0 — Production Readiness

The 1.0 track is stability-first and intentionally does not add another language feature wave.

| Batch | Scope | Status |
| --- | --- | --- |
| 76 | solution-wide verification surface + release scripts | IMPLEMENTED / NOT VERIFIED |
| 77 | durable reconciliation baseline/state | PLANNED |
| 78 | explicit conflict policies | PLANNED |
| 79 | generic reconciliation mutator contract | PLANNED |
| 80 | leases/fencing + duplicate concurrent reconciliation protection | PLANNED |
| 81 | crash/restart semantics + checkpoints | PLANNED |
| 82 | secure host/network/file policy hardening | PLANNED |
| 83 | telemetry/observability contract | PLANNED |
| 84 | stress/property/invariant tests | PLANNED |
| 85 | language/API/serialization contract freeze | PLANNED |
| 86 | packaging + CLI consolidation | PLANNED |
| 87 | upgrade/backward-compatibility tests | PLANNED |
| 88 | 1.0 RC ledger | PLANNED |
| 89 | final verification/release gate | PLANNED |

No public version promotion or release claim occurs until Batch 89 has successful evidence for the exact candidate tree.
