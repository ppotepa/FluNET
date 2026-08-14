# Status and limitations

This file is the authoritative support matrix for the current `main` source tree.

## Version / release status

- Public built-in `StandardLanguageIdentity.Version`: **0.3**.
- The tree contains source-level compiler/data/automation work beyond that published version.
- Milestone documents describe 0.4/0.6/0.7 **freeze candidates**, not released compatibility versions.
- No successful Release restore/build/test run has been established for the exact current tree in this documentation update.
- The current GitHub commit status endpoint reports no status checks for the pre-docs head; do not interpret that as a passing CI result.

Release gate before changing the public version or creating a release/tag:

```bash
dotnet restore FluNET.sln
dotnet build FluNET.sln --configuration Release --no-restore
dotnet test FluNET.sln --configuration Release --no-build
```

## Support matrix

| Area | Current status | Notes |
| --- | --- | --- |
| Canonical commands | implemented source API/CLI | GET, SAVE, LOAD, DELETE, DOWNLOAD, POST, SAY, SEND, TRANSFORM, SET, PARSE, FORMAT frames. |
| Canonical AND/THEN/ELSE | implemented | Explicit orchestration/control syntax. |
| Conditions/expressions | implemented | Boolean/comparison/arithmetic, property/index access, `??`, conditions. |
| Retry/timeout/error policy | implemented | Canonical modifiers and compact directives/profiles. |
| Typed compilation | implemented | Parse -> Bind -> Validate -> Compile -> TypeCheck -> Plan -> Execute. |
| Structural type system | implemented | List/Map/Optional/Union/object fields, explicit conversions. |
| Compact parser/lowering | implemented source API/CLI tools | `flunet check/fmt/explain/graph/run`. |
| Compact LOAD | implemented for JSON/text | JSON glob also available. |
| Compact GET file | implemented for supported file decoders | JSON/text. |
| Compact GET HTTP | implemented for JSON | Not a generic HTTP content-type layer. |
| Compact GET environment | implemented | `env:NAME`. |
| Compact GET secret | implemented, denied by default | Host must supply store + allow policy. |
| Property/index interpolation | implemented | e.g. `{post.title}`, `posts[0].title`. |
| Automatic data dependencies | implemented | Dependency graph + effect metadata. |
| Explicit/implicit pipelines | implemented | Ordinary typed variables between stages. |
| FILTER/SORT/TAKE | implemented | `List<Json>` transforms. |
| SELECT/MAP/DEFAULT | implemented | JSON projections / top-level defaults. |
| GROUP/SUM/JOIN/MATCH | implemented | Pure typed JSON transforms. |
| FOR EACH | **partial** | Bounded concurrency; body currently supports SAY actions only. |
| TASK/RUN | implemented source compiler | Compile-time hygienic expansion, bounded recursion depth. |
| POLICY/WITH | implemented | RETRY/TIMEOUT/CONTINUE profiles. |
| CACHE | implemented | Read/Pure only; literal/resource inputs; default in-memory cache. |
| ONCE BY | implemented | Write/mutation only; default in-memory idempotency store. |
| Pluggable resource providers | implemented | `module.ResourceProvider<TProvider>()`. |
| Schema inference | implemented API | Side-effect-free inference from supplied JSON sample/declared data. |
| EVERY/WATCH/WHEN compiler | implemented embedding API | Separate automation compiler, not `flunet run`. |
| Automation scheduler | implemented host-driven API | No hidden background thread. |
| Durable automation timer state | implemented single-host store | Definitions must be re-registered after restart. |
| ENSURE compilation | implemented embedding API | Goal -> ordinary GET/SAVE plan. |
| ENSURE runtime execution | experimental source API | Version retention + failure notifier hooks exist; release verification still required. |
| Durable workflow journal | implemented single-host stores | Checksummed durable store and resume protocol. |
| CSV/XML compact decoder | **not implemented** | Extensions are recognized by inference but no built-in decoder/provider path. |
| Generic binary/image compact value decoder | **not implemented** | File format can be classified, not decoded as a language value. |
| SQL provider | **not implemented built-in** | `sql:` classification exists but no built-in executable provider. |
| Generic AUTH compact directive | **not implemented** | Current lowerer emits a diagnostic. |
| Arbitrary FOR EACH body actions | **not implemented** | SAY-only current contract. |
| BACKOFF policy | **not implemented** | Reserved until execution-policy contract supports it. |
| `CONTINUE ON 404` / status-specific policy | **not implemented** | Use general CONTINUE/CONTINUE ON ERROR. |
| Cron expressions | **not implemented** | Automation supports fixed intervals. |
| Internal always-running scheduler | intentionally absent | Host owns timer/signal delivery. |
| Generic SYNC/reconciliation | **not present as public current feature** | Do not rely on earlier roadmap/experimental descriptions. |
| Generic compensation syntax | **not documented/supported as public compact language** | Do not assume rollback for arbitrary effects. |
| Public audit/history query API | **not established as a supported current feature** | Workflow journals exist, but not a general audit product surface. |
| Distributed workflow coordination/leases | **not established in current public runtime** | Current durable workflow target is single-host. |

## Important compact-language limitations

### File decoders

Resource inference recognizes extensions such as JSON, CSV, XML, text, binary and common image formats. That does **not** mean every inferred format has an executable built-in decoder.

Current compact file provider execution:

- JSON -> yes;
- text (`.txt`, `.md`, `.log`) -> yes;
- JSON glob -> yes;
- CSV/XML/binary/image -> no built-in value decoder.

### HTTP

Compact HTTP GET currently expects/returns JSON. Non-JSON inferred HTTP format is rejected rather than guessed.

### FOR EACH

Current surface contract:

```text
FOR EACH item [PARALLEL n]
    SAY ...
```

- default concurrency 4;
- explicit range 1..256;
- body currently SAY-only.

### CACHE

CACHE is not a general memoization oracle. Current constraints intentionally keep the key complete:

- eligible effects: Read/Pure;
- cache input must currently be literal/resource based, not variable based;
- default cache is process-local/in-memory.

### ONCE BY

The default idempotency store is process-local/in-memory. If `ONCE BY` must survive restart, a host must provide another `IIdempotencyStore` implementation.

### Secrets

The default host cannot read secrets:

```text
ISecretStore = EmptySecretStore
ISecretAccessPolicy = DenyAllSecretAccessPolicy
```

Secrets are opaque and not implicitly convertible to Text.

## CLI boundaries

### Canonical prompt mode

```text
flunet [options] -- "PROMPT"
```

- file access defaults to current working directory;
- `--root PATH` can add/restrict file roots;
- if no `--host` is supplied, the current CLI leaves network access unrestricted;
- supplying `--host` values restricts network access to those hosts.

### Surface file tools

```text
flunet check FILE
flunet fmt FILE
flunet explain FILE
flunet graph FILE
flunet run FILE
```

The current surface-tool path uses the current directory as its file root and open network access. It does not currently expose the canonical prompt runner's `--root`/`--host` option parsing for those subcommands.

Automation and ENSURE are embedding APIs, not CLI subcommands.

## Durability boundaries

`DurableWorkflowStateStore` is a **single-host** durable journal. It provides checksums, append-only event storage and disk flushing, but it is not a distributed transaction/lease system.

`DurableAutomationScheduleStore` persists interval schedule state, not compiled automation definitions. Definitions must be reconstructed and re-registered by the host after restart.

## Security model

FluNET does not make an untrusted prompt safe by itself. Production hosts should explicitly configure:

- `IExecutionPolicy`;
- file roots;
- network hosts/transport;
- secret store + secret allow policy;
- custom resource-provider capabilities;
- process/container isolation where appropriate.

The embedding API's default `AllowAllExecutionPolicy` is maintained for backward compatibility and should not be treated as a hardened production policy.

## Compatibility status

Legacy sentence/token-tree APIs remain for source compatibility. New development should use:

- stable command/frame/type identities;
- typed binders/handlers;
- value codecs/conversions;
- resource-provider registration;
- surface compiler for compact syntax.

See [legacy API migration](legacy-api-migration.md).

## Milestone notes vs current contract

The milestone readiness files under `docs/` document architecture progress. The current code and this support matrix take precedence when a roadmap concept was discussed but is not actually present in the current `main` public surface.