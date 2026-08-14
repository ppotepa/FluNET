# FluNET master roadmap

Status vocabulary: **IMPLEMENTED** means the source contract exists on `main`; **VERIFIED** requires a successful Release restore/build/test against the exact tree; **FROZEN/RELEASED** additionally requires an explicit version promotion. Public `StandardLanguageIdentity.Version` remains `0.3` until that gate is satisfied.

## 0.8 — Integration & Execution

Batches **52–65: IMPLEMENTED source candidate / NOT VERIFIED**.

52 roadmap · 53 resource payload/decoders · 54 CSV/XML · 55 Binary/Image · 56 typed HTTP media · 57 SQL · 58 AUTH · 59 nested action IR · 60 full FOR EACH actions · 61 policy v2 · 62 durable cache/idempotency · 63 calendar/cron · 64 automation/ENSURE CLI adapters · 65 tooling/freeze ledger.

See [0.8 freeze readiness](compiler-0.8-freeze-readiness.md).

## 0.9 — Declarative Reconciliation

Batches **66–75: IMPLEMENTED source candidate / NOT VERIFIED**.

66 Desired/Observed State IR · 67 pluggable observation · 68 SYNC compiler · 69 keyed diff/conflicts · 70 reconciliation runner · 71 WATCH bridge · 72 compensation · 73 saga · 74 redacted history · 75 freeze ledger.

See [0.9 freeze readiness](compiler-0.9-freeze-readiness.md).

## 1.0 — Production Readiness

Batches **76–88: IMPLEMENTED RC source candidate / NOT VERIFIED**. Batch 89 is the only remaining release gate and cannot be marked complete without real build/test evidence.

| Batch | Scope | Status |
| --- | --- | --- |
| 76 | solution-wide Release gate + bash/PowerShell scripts | IMPLEMENTED / NOT VERIFIED |
| 77 | durable reconciliation baseline/state | IMPLEMENTED |
| 78 | `ON CONFLICT FAIL/KEEP TARGET/KEEP SOURCE` | IMPLEMENTED |
| 79 | generic reconciliation mutator registry | IMPLEMENTED |
| 80 | target leases, heartbeat and monotonic fencing tokens | IMPLEMENTED |
| 81 | crash/restart checkpoints + atomic physical SAVE | IMPLEMENTED |
| 82 | opt-in secure host, DNS/private-address and redirect hardening | IMPLEMENTED |
| 83 | metadata-only command/reconciliation telemetry | IMPLEMENTED |
| 84 | property/stress/corruption/concurrency contracts | IMPLEMENTED source tests |
| 85 | extension/persistence/durable-format contract freeze candidate | IMPLEMENTED |
| 86 | `FluNET.Tool` packaging + compatibility CLI boundary | IMPLEMENTED |
| 87 | backward-compatibility/upgrade contracts | IMPLEMENTED source tests |
| 88 | RC readiness ledger | IMPLEMENTED / NOT VERIFIED |
| 89 | exact-tree Release verification + version/package promotion decision | BLOCKED ON VERIFICATION |

See [1.0 RC readiness](1.0-rc-readiness.md) and [verification gate](1.0-verification.md).

No public version promotion or release claim occurs until Batch 89 has successful evidence for the exact candidate tree.
