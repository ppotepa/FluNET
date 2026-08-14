# FluNET 0.8 Integration & Execution freeze readiness

**Status: IMPLEMENTED SOURCE CANDIDATE — NOT VERIFIED / NOT FROZEN.**

Public `StandardLanguageIdentity.Version` intentionally remains `0.3`. This document records source implementation progress; it is not a release claim.

## Implemented 0.8 batches

- Batch 52 — master roadmap and release ledger;
- Batch 53 — `ResourcePayload`, decoder/encoder registries and module registration;
- Batch 54 — executable CSV/XML compact reads;
- Batch 55 — first-class `Binary` and `Image` language values;
- Batch 56 — HTTP status/media/charset/header response model and typed JSON/Text/CSV/XML/Binary/Image decoding;
- Batch 57 — `sql:` provider with host-supplied `ISqlQueryExecutor` and ADO.NET adapter;
- Batch 58 — lexical `AUTH secret:name`, opaque secret binding and authenticated HTTP capability;
- Batch 59 — compiled nested action templates and isolated action scopes;
- Batch 60 — `FOR EACH item IN collection PARALLEL n` with GET/LOAD/SAVE/POST/SAY actions;
- Batch 61 — fixed/exponential backoff, deterministic jitter and status-specific retry/continue/fail matching;
- Batch 62 — opt-in durable cache and idempotency stores;
- Batch 63 — interval/daily/weekly/cron automation schedules with timezones;
- Batch 64 — automation and ENSURE CLI adapters;
- Batch 65 — expanded explain/graph tooling and this freeze ledger.

## Architectural invariants

- providers acquire data; decoders interpret data;
- Secret remains opaque and has no implicit Secret -> Text conversion;
- nested workflows use explicit local action scopes;
- source order/newlines/`;` do not create unnecessary execution ordering;
- automation scheduler remains host-driven;
- all executable paths ultimately use the same typed command/planning/execution stack.

## Release gate

Before any 0.8 version/tag/freeze claim, the exact candidate tree must pass:

```bash
dotnet restore FluNET.sln
dotnet build FluNET.sln --configuration Release --no-restore
dotnet test FluNET.sln --configuration Release --no-build
```

Until that evidence exists, this milestone remains **IMPLEMENTED, NOT VERIFIED**.
