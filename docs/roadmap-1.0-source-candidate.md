# FluNET roadmap — 1.0 source candidate

This is the final source-implementation ledger after the 0.8 / 0.9 / 1.0 roadmap pass.

Legend: **IMPLEMENTED** means source exists on `main`; **VERIFIED** requires the Release build/test gate; **FROZEN/RELEASED** additionally requires explicit version promotion. Public `StandardLanguageIdentity.Version` is still `0.3`.

## 0.8 — Integration & Execution

| Batch | Scope | Status |
| --- | --- | --- |
| 52 | master roadmap / release ledger | IMPLEMENTED |
| 53 | resource payload + decoder/encoder registries | IMPLEMENTED |
| 54 | CSV / XML | IMPLEMENTED |
| 55 | Binary / Image language values | IMPLEMENTED |
| 56 | generic typed HTTP media decoding | IMPLEMENTED |
| 57 | SQL provider boundary | IMPLEMENTED |
| 58 | AUTH / opaque HTTP credentials | IMPLEMENTED |
| 59 | compiled nested action IR | IMPLEMENTED |
| 60 | full `FOR EACH ... IN ... PARALLEL n` action bodies | IMPLEMENTED |
| 61 | policy v2: backoff/jitter/status matchers | IMPLEMENTED |
| 62 | durable cache / idempotency | IMPLEMENTED |
| 63 | daily/weekly/cron schedules + timezone | IMPLEMENTED |
| 64 | automation / ENSURE CLI adapters | IMPLEMENTED |
| 65 | 0.8 tooling + freeze ledger | IMPLEMENTED / NOT VERIFIED |

## 0.9 — Declarative Reconciliation

| Batch | Scope | Status |
| --- | --- | --- |
| 66 | Desired / Observed State IR | IMPLEMENTED |
| 67 | resource observation | IMPLEMENTED |
| 68 | `SYNC target WITH source BY key` compiler | IMPLEMENTED |
| 69 | keyed diff IR + three-way conflicts | IMPLEMENTED |
| 70 | reconciliation mutation planner / runner | IMPLEMENTED |
| 71 | WATCH / WHEN reconciliation signal bridge | IMPLEMENTED |
| 72 | explicit compensation contracts | IMPLEMENTED |
| 73 | saga orchestration | IMPLEMENTED |
| 74 | workflow audit / history API | IMPLEMENTED |
| 75 | 0.9 freeze ledger | IMPLEMENTED / NOT VERIFIED |

## 1.0 — Stable Platform candidate

| Batch | Scope | Status |
| --- | --- | --- |
| 76 | language contract manifest | IMPLEMENTED |
| 77 | extension API contract manifest | IMPLEMENTED |
| 78 | security capability manifest | IMPLEMENTED |
| 79 | persistence contract inspector | IMPLEMENTED |
| 80 | compatibility boundary ledger | IMPLEMENTED |
| 81 | logical platform/module topology | IMPLEMENTED |
| 82 | complete candidate CLI (`src/FluNET.Tool`) | IMPLEMENTED |
| 83 | static release contract verifier + verification gate | IMPLEMENTED / NOT VERIFIED |

## What remains before a public 1.0

No new roadmap feature batch is required by this ledger. Remaining work is evidence and stabilization:

```bash
dotnet restore FluNET.sln
dotnet build FluNET.sln --configuration Release --no-restore
dotnet test FluNET.sln --configuration Release --no-build
dotnet build src/FluNET.Tool/FluNET.Tool.csproj --configuration Release
```

Any compiler/test failures become stabilization commits. Only after the exact tree is green should the public language identity move from `0.3` to `1.0`, followed by another full verification run and an explicitly requested release/tag.
