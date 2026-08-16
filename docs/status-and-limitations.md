# Status and limitations

This is the current source support matrix for `main`. The implementation is a **verified source candidate**. Public `StandardLanguageIdentity.Version` remains **0.3** and the Tool package remains preview until explicit version promotion.

## Implemented source capabilities

| Area | Status |
| --- | --- |
| typed compiler/type system/conversion graph | implemented |
| canonical AND/THEN/IF/ELSE and execution policies | implemented |
| conversational control flow | implemented: `IF`/`UNLESS`/`ELSE IF`/`ELSE`, bounded `WHILE`, `BREAK WHEN`, `CONTINUE WHEN` |
| compact syntax, `,`, `;`, `|`, inference/lowering | implemented |
| conversational assignment sugar | implemented: `LET name = value` and `SET name TO value` lower to typed assignments |
| compact QoL: `WHERE`, `ORDER BY`, implicit sinks, `LET`, `?.`, `??` | implemented |
| compact collections: `DISTINCT`, `SKIP`, `COUNT`, `AVG`, `MIN`, `MAX` | implemented |
| automatic JSON sink encoding for `SAVE TO *.json` | implemented |
| clipboard input/output | implemented through the cross-platform `system.clipboard` provider; workflows can use `READ CLIPBOARD AS value` and `COPY value TO CLIPBOARD`, and CLI supports `:paste` (Linux uses wl-paste/xclip/xsel when installed) |
| portable time and delay | implemented through `system.time`; `NOW` uses an injectable clock and `WAIT` uses a cancellation-aware delay provider |
| portable filesystem toolkit: scan/find, hash, file and directory copy/move, recoverable file/directory trash and restore, watch, ZIP, directories | implemented through capability providers |
| compact file queries | `FIND ... WHERE ...` lowers into the existing scan + typed JSON filter pipeline; multiline `WHERE`, ordering and bounded collection operators remain composable |
| file content search | `SEARCH ... IN ...` supports recursive plain-text/regex matching, binary skipping and provider-side `LIMIT` |
| readable file predicates | `CONTAINS`, `STARTS WITH`, `ENDS WITH`, glob `MATCHES`, metadata aliases and `AFTER`/`BEFORE` timestamps are supported by the same provider-neutral filter stage |
| archive inspection | `LIST ARCHIVE ... WHERE/ORDER BY/TAKE/SKIP` exposes ZIP/TAR/TAR.GZ entry metadata without extraction |
| CSV export | `SAVE CSV [rows] TO ...` writes object rows with inferred headers and RFC 4180 escaping |
| portable file metadata queries | `SCAN`/`FIND` and `LIST` expose name, stem, parent, relative path, timestamps, hidden/read-only flags and size; `LIMIT` is enforced by the provider |
| reusable file metadata index | `INDEX FILES [files] FROM {./data} RECURSIVE` and `READ INDEX [files] FROM {./data}` use an in-memory or durable SQLite provider; SQLite applies allow-listed `WHERE`, `ORDER BY`, `TAKE` and `SKIP` clauses in the database |
| object storage adapters | blob contract supports memory, local files, HTTP gateways and S3-compatible SigV4 endpoints (AWS/MinIO style) |
| embedding host factory | `FluNetHost.Create` wires policy-aware portable defaults while preserving DI overrides |
| reference application | `samples/FluNET.HostedApp` demonstrates a minimal embeddable host and canonical prompt execution |
| host diagnostics | `DOCTOR [report]` reports runtime, platform, capabilities and active provider types without exposing secrets |
| NuGet packaging | `FluNET.Engine` emits a package with README/license metadata; CLI and sample remain repository applications |
| live index synchronization | host-controlled `FileMetadataIndexWatcher` applies create/change/delete/rename deltas where supported, with provider rebuild fallback |
| compact directory queries | `LIST` and `FIND` support `WHERE`, `ORDER BY`, `TAKE` and `SKIP` through the same typed collection stages |
| local key-value, SQLite and object storage | implemented; durable backends are opt-in (`--store PATH` / `--sqlite PATH` / `--blob PATH`), with injectable HTTP blob provider seam |
| blob listing | `LIST BLOB "prefix/" AS keys` returns provider-neutral `{key}` rows for memory, file and HTTP object stores |
| key-value listing | `LIST STORE "prefix" AS values` returns provider-neutral `{key,value}` rows for memory and JSON-file stores |
| key-value deletion | `DELETE STORE "key" AS removed` removes a key through the storage provider |
| authenticated blob gateway | HTTP blob requests can use a host-owned `SecretValue` and authentication scheme |
| direct process execution | implemented; deny-by-default, explicit host/CLI opt-in, portable `IN` working directory, explicit `ENV` overrides and registry-owned `START`/`SEND`/`STOP` sessions |
| portable special paths | implemented through `system.path`; `PATH TEMP`, `PATH HOME`, `PATH CACHE` and other host folders use an injectable resolver |
| environment mutation | implemented through `system.environment.write`; denied by default and enabled explicitly by host policy or CLI `--allow-environment-write` |
| portable temporary artifacts | implemented through `system.temp`; `TEMP FILE`, optional suffixes, `TEMP DIRECTORY`, and ownership-checked `CLEANUP` create and remove policy-checked host temp paths |
| HTTP capability discovery and mutations | implemented through `network.http`; GET/POST/PUT/PATCH/DELETE and bounded JSON pagination use the provider boundary |
| event sinks | `EMIT ... TO ...` uses a host-owned `IFluNetEventSink`; the default is an HTTP webhook and custom brokers can replace it |
| secret backends | `EnvironmentSecretStore` and priority-ordered `CompositeSecretStore` are available; access remains controlled by `ISecretAccessPolicy` |
| host configuration | `GET config:NAME` resolves through `IFluNetConfiguration`; default hosts miss keys until a provider is registered |
| remote messaging | `HttpFluNetMessageBus` provides a provider-neutral REST queue adapter; broker-specific hosts can replace `IFluNetMessageBus` |
| durable messaging | `--queue *.db`/`*.sqlite` selects the transactional SQLite queue; other queue paths retain the atomic JSONL backend |
| module capability providers | implemented; modules register provider-neutral capabilities through `FluNetModuleBuilder.Capability<TProvider>()` |
| host notifications and messaging | implemented through `system.notify` and `messaging.queue`; in-memory and single-host JSONL queue backends are available, and hosts can replace them through DI |
| automation event metadata | implemented through `AutomationSignal`; file-watch adapters preserve path, oldPath, kind, timestamp, directory flag, and length in each run result |
| durable automation signals | implemented through `IAutomationSignalStore` with in-memory, JSONL and SQLite adapters; `watch --events PATH` persists and `automation replay` re-delivers signals in journal order |
| JSON/Text/CSV/XML/Binary/Image resources | implemented |
| typed HTTP, SQL, env, secret, AUTH | implemented; SQL/secret and authenticated mutations need host capability configuration |
| parameterized SQL | `$name` placeholders resolve registered FluNET variables and bind through `DbParameter` for SQLite, generic ADO.NET and `DbProviderFactory` adapters |
| explicit SQL mutations | `APPLY SQL ... AS affected` executes through the provider and returns the affected row count |
| data transforms + bounded nested FOR EACH actions | implemented; resource, HTTP and filesystem actions use shared providers |
| TASK, POLICY, backoff/jitter/status matchers | implemented |
| CACHE / ONCE BY + durable stores | implemented; durable variants are opt-in |
| EVERY/WATCH/WHEN + calendar/cron | implemented host-driven automation; file watch options provide filtering, recursion, debounce and structured change metadata |
| ENSURE | implemented source compiler/runner |
| durable workflow journal/history | implemented single-host stores |
| SYNC desired/observed reconciliation | implemented source candidate |
| durable reconciliation baselines | implemented single-host store |
| three-way conflicts + explicit conflict policies | implemented |
| generic reconciliation mutator registry | implemented; built-in concrete mutator is local JSON replacement |
| WATCH-triggered reconciliation | implemented host-driven signal bridge |
| compensation + saga | explicit local SAVE inverse; arbitrary external rollback intentionally unsupported |
| reconciliation leases/fencing | in-memory and shared-filesystem implementations |
| crash/restart reconciliation checkpoints | implemented; restart re-observes instead of blindly replaying mutation |
| atomic physical file replacement | implemented for built-in physical writes |
| secure host profile | opt-in; HTTPS, allow-list, DNS/private-IP, redirect and path hardening |
| telemetry | opt-in metadata-only sink; no OpenTelemetry dependency in core |
| `FluNET.Tool` | in solution, `net9.0`, configured as local .NET tool preview |
| stress/property/backward-compatibility contracts | backward-compatibility and source contracts execute in the formal Release gate; dedicated stress/property suites remain future work |

## Intentional boundaries

- There is still one typed command execution stack; reconciliation mutators produce ordinary plans.
- The built-in reconciliation mutator currently owns local JSON replacement. HTTP/SQL/custom targets should register explicit `IReconciliationMutator` implementations.
- Shared-filesystem fencing is only useful when external/custom mutators propagate and enforce the exposed fencing token where their target supports it.
- Secure hosting is opt-in so compatibility hosts are not silently broken.
- Built-in durable state is single-host/shared-filesystem oriented, not a consensus database or distributed transaction manager.
- Compensation is a saga-style inverse contract, not ACID rollback.

## Verification status

Batches 76–88 describe the release-candidate scope. The current working tree passed the complete local gate: warning-free Release build, 638 passing tests, Tool smoke checks, package creation and local Tool installation. GitHub status remains external evidence and is not inferred from this local result.

Canonical gate:

```bash
./scripts/verify-release.sh
```

or:

```powershell
./scripts/verify-release.ps1
```

No language/package version promotion or release tag should occur without successful evidence for the exact tree.
