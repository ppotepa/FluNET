# FluNET master roadmap

This file is the authoritative implementation ledger after the 0.3 published identity. A feature is not a released language guarantee merely because its source implementation exists.

Status values:

- **PLANNED** — designed, not yet merged;
- **IMPLEMENTED** — source implementation is on `main`;
- **VERIFIED** — the exact tree passed the required Release restore/build/test gate;
- **FROZEN** — compatibility contract/version snapshot is intentionally frozen.

## Existing source milestones

| Milestone | Scope | Status |
| --- | --- | --- |
| 0.4 | typed compiler core, conversions, variables, expressions, type checking | IMPLEMENTED, not release-verified |
| 0.5 | compact syntax, inference/lowering, contexts, tooling, `,`, `;`, `|` | IMPLEMENTED, not release-verified |
| 0.6 | typed JSON data language and schema inference | IMPLEMENTED, not release-verified |
| 0.7 | tasks, policies, providers, cache/idempotency, secrets, automation, ENSURE foundation | IMPLEMENTED/experimental, not release-verified |

Public `StandardLanguageIdentity.Version` remains `0.3` until an exact candidate tree passes the release gate.

## 0.8 — Integration & Execution

| Batch | Scope | Status |
| --- | --- | --- |
| 52 | roadmap/release baseline | IMPLEMENTED |
| 53 | unified resource payload + decoder/encoder registry | PLANNED |
| 54 | CSV/XML decoders | PLANNED |
| 55 | Binary/Image language values | PLANNED |
| 56 | generic HTTP response/media model | PLANNED |
| 57 | SQL provider | PLANNED |
| 58 | authentication profiles and secret binding | PLANNED |
| 59 | compiled nested action model | PLANNED |
| 60 | full `FOR EACH` workflow bodies | PLANNED |
| 61 | policy model 2: backoff/jitter/error matchers | PLANNED |
| 62 | durable cache/idempotency stores | PLANNED |
| 63 | calendar/cron automation triggers | PLANNED |
| 64 | automation/ENSURE CLI | PLANNED |
| 65 | 0.8 tooling + freeze candidate docs | PLANNED |

## 0.9 — Declarative Reconciliation

| Batch | Scope | Status |
| --- | --- | --- |
| 66 | Desired/Observed State IR | PLANNED |
| 67 | resource observation contracts | PLANNED |
| 68 | `SYNC` compiler | PLANNED |
| 69 | keyed diff IR | PLANNED |
| 70 | reconciliation planner/runner | PLANNED |
| 71 | WATCH reconciliation bridge | PLANNED |
| 72 | compensation contracts | PLANNED |
| 73 | saga execution | PLANNED |
| 74 | audit/history API | PLANNED |
| 75 | 0.9 freeze candidate docs | PLANNED |

## 1.0 — Stable Platform

| Batch | Scope | Status |
| --- | --- | --- |
| 76 | public language contract manifest | PLANNED |
| 77 | extension API contract manifest | PLANNED |
| 78 | host security contract | PLANNED |
| 79 | persistence contract | PLANNED |
| 80 | compatibility/deprecation cleanup | PLANNED |
| 81 | package/module boundary preparation | PLANNED |
| 82 | complete CLI command surface | PLANNED |
| 83 | cross-platform verification and 1.0 release gate | PLANNED |

## Release gate

A milestone cannot become VERIFIED/FROZEN until the exact candidate tree passes:

```bash
dotnet restore FluNET.sln
dotnet build FluNET.sln --configuration Release --no-restore
dotnet test FluNET.sln --configuration Release --no-build
```

No source-only implementation step may silently bump a public language version or claim a passing CI result.
