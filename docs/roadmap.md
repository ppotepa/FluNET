# FluNET master roadmap

Status vocabulary: **IMPLEMENTED** means the source contract exists on `main`; **VERIFIED** requires a successful Release restore/build/test against the exact tree; **FROZEN/RELEASED** additionally requires an explicit version promotion. Public `StandardLanguageIdentity.Version` remains `0.3` until that gate is satisfied.

## 0.8 — Integration & Execution

Batches **52–65: IMPLEMENTED source candidate / NOT VERIFIED**.

See [0.8 freeze readiness](compiler-0.8-freeze-readiness.md).

## 0.9 — Declarative Reconciliation

Batches **66–75: IMPLEMENTED source candidate / NOT VERIFIED**.

See [0.9 freeze readiness](compiler-0.9-freeze-readiness.md).

## 1.0 — Production Readiness

Batches **76–88: IMPLEMENTED RC source candidate / NOT VERIFIED**. Batch 89 has implemented the release-gate policy and recorded the current absence of verification evidence; release remains blocked.

| Batch | Scope | Status |
| --- | --- | --- |
| 76 | solution-wide Release gate + bash/PowerShell scripts | IMPLEMENTED / NOT VERIFIED |
| 77 | durable reconciliation baseline/state | IMPLEMENTED |
| 78 | explicit reconciliation conflict policies | IMPLEMENTED |
| 79 | generic reconciliation mutator registry | IMPLEMENTED |
| 80 | leases, heartbeat and monotonic fencing tokens | IMPLEMENTED |
| 81 | crash/restart checkpoints + atomic physical SAVE | IMPLEMENTED |
| 82 | secure host/network/file hardening | IMPLEMENTED |
| 83 | metadata-only telemetry | IMPLEMENTED |
| 84 | stress/property/corruption/concurrency contracts | IMPLEMENTED source tests |
| 85 | language/API/persistence/durable-format freeze candidate | IMPLEMENTED |
| 86 | Tool packaging + compatibility CLI boundary | IMPLEMENTED |
| 87 | upgrade/backward-compatibility contracts | IMPLEMENTED source tests |
| 88 | RC source-readiness ledger | IMPLEMENTED / NOT VERIFIED |
| 89 | release-promotion gate policy + evidence decision | GATE IMPLEMENTED / RELEASE BLOCKED |

See [1.0 RC source readiness](1.0-rc-readiness.md), [verification gate](1.0-verification.md), and [release gate](1.0-release-gate.md).

The next action is not another feature batch. It is to run the canonical gate in a .NET 9 environment, fix any failures, rerun it, promote the public version only after green pre-promotion evidence, and rerun again before an explicit release/tag.
