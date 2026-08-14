# Status and limitations

This file is the current support matrix for `main`. Public `StandardLanguageIdentity.Version` is still **0.3** because the exact current tree has not passed the formal Release restore/build/test gate.

## Implemented compiler / compact / integration surface

| Area | Status |
| --- | --- |
| typed compiler/type system/conversion graph | implemented |
| canonical AND/THEN/IF/ELSE and execution policies | implemented |
| compact syntax, `,`, `;`, `|`, inference/lowering | implemented |
| check/fmt/explain/graph/run | implemented |
| JSON/Text/CSV/XML local decoding | implemented |
| Binary/Image values and local decoding | implemented |
| generic typed HTTP media decoding | implemented |
| `env:`, `secret:`, `sql:` providers | implemented; secret/SQL require host capability configuration |
| lexical `AUTH secret:name` | implemented for HTTP |
| FILTER/SORT/TAKE/SELECT/MAP/DEFAULT/GROUP/SUM/JOIN/MATCH | implemented |
| `FOR EACH item IN collection PARALLEL n` | implemented with GET/LOAD/SAVE/POST/SAY nested actions |
| TASK/RUN | implemented compile-time expansion |
| POLICY/WITH + BACKOFF/JITTER/status matching | implemented |
| CACHE / ONCE BY | implemented; durable stores are opt-in single-host implementations |
| EVERY/WATCH/WHEN + calendar/cron | implemented host-driven automation |
| ENSURE | implemented source/embedding path with refresh/version/failure hooks |
| durable workflow journal | implemented single-host checksummed store |

## 0.9 declarative reconciliation

| Area | Current 0.9 source contract |
| --- | --- |
| Desired/Observed State | keyed JSON object records with canonical fingerprints and duplicate-key rejection |
| resource observation | pluggable `IResourceObserverRegistry` |
| local observation | JSON, CSV, XML and text resources that normalize to object records |
| HTTP observation | typed JSON/CSV/XML/text decoding through the existing HTTP + decoder boundaries |
| SQL observation | rows from `ISqlQueryExecutor` normalize to JSON records |
| environment observation | synthetic `{ name, value }` record |
| secret observation | synthetic `{ name, fingerprint }` record; plaintext is not put in reconciliation state |
| SYNC | `SYNC target WITH source BY key`; right side is authoritative source-of-truth |
| diff | Create / Update / Delete / Unchanged plus optional three-way Conflict |
| reconciliation execution | source and target are observed concurrently, then diffed |
| built-in mutation | local JSON target only; desired snapshot is written through ordinary compact `SAVE` / `ExecutionPlanExecutor` |
| missing local target | treated as empty observed state and can be created by reconciliation |
| WATCH reconciliation | host-driven WATCH/WHEN signal bridge invokes compiled SYNC definitions |
| compensation | explicit `COMPENSATE`; built-in deterministic inverse currently supports literal local SAVE only |
| saga | multiple ordinary plan units share a compensation journal and reverse successful reversible effects on later failure |
| audit/history | redacted workflow event projection, summaries and optional durable run catalog |

## 0.9 limitations

### Mutation providers

Observation is pluggable, but 0.9 does **not** yet expose a general mutation-provider registry. Built-in SYNC mutation deliberately supports a single local JSON file target. HTTP/SQL/custom targets require a later explicit mutation contract rather than hidden side effects.

### Baselines and conflicts

Two-way SYNC is the default. Three-way conflict detection is available when a host passes a baseline snapshot to `ReconciliationRunner.RunAsync`. 0.9 does not automatically persist or select reconciliation baselines between runs.

### Record shape

Reconciliation operates on JSON object records identified by one top-level scalar key field. Binary/Image payloads are not reconciliation record sets. CSV/XML/text are usable only when their decoder result can normalize to object records containing the requested key.

### Secrets

Secret observation requires the normal secret access policy and exposes only a SHA-256 fingerprint plus the secret name to state comparison. Secret plaintext is never stored in a reconciliation snapshot or audit projection by this path.

### Compensation

`COMPENSATE` is not an ACID transaction and does not promise rollback for arbitrary effects. Built-in compensation currently accepts literal local SAVE only. POST, email and other effects without a deterministic inverse are rejected instead of receiving pretend rollback semantics.

### WATCH

The reconciliation WATCH scheduler is host-driven. It owns no hidden watcher thread or polling loop; the host publishes resource/event signals.

### Audit and durability

Durable journals and run catalogs target one host. Audit events hash result payloads rather than exposing raw `ResultJson`, but this is not a distributed audit database or distributed transaction system.

## CLI boundaries

The stable compatibility CLI remains `FluNET.Cli`. Reconciliation is available through the embedding APIs (`CompileSync`, `ExecuteSyncAsync`, WATCH scheduler). The separate `FluNET.Tool` candidate also exposes `sync check|apply`, but its 1.0 packaging/integration is outside the 0.9 freeze.

## Security boundaries

Default embedding policy remains permissive for backwards compatibility. Default secret access is deny-all and SQL is unconfigured/denied. Production hosts should explicitly configure file, network, secret and database capabilities.

## Verification gate

```bash
dotnet restore FluNET.sln
dotnet build FluNET.sln --configuration Release --no-restore
dotnet test FluNET.sln --configuration Release --no-build
```

No language-version bump, freeze claim or release tag should occur without successful evidence for the exact tree.
