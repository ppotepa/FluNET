# FluNET

[![CI](https://github.com/ppotepa/FluNET/actions/workflows/ci.yml/badge.svg)](https://github.com/ppotepa/FluNET/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)

FluNET is an experimental .NET DSL/compiler/runtime for readable local automation and typed data workflows. It supports an explicit **canonical syntax** and a higher-level **compact syntax** that can infer obvious resource types, output names and data dependencies before lowering to the same typed execution engine.

```text
LOAD post.json, todo.json
SAY "{post.title} — {todo.title}"
```

The compiler can infer that the two reads are independent, schedule them together and make `SAY` depend on both outputs. You can still use explicit canonical `AND`/`THEN` when you want exact control edges.

> **Project status:** the public built-in `LanguageVersion` is still `0.3`. `main` contains source-level compact/data/automation work beyond that published version, but the exact tree has not yet been release-verified with a successful Release restore/build/test run. Treat advanced surfaces as experimental until that gate is complete.

## Quick start

Requirement: .NET 8 SDK.

```bash
git clone https://github.com/ppotepa/FluNET.git
cd FluNET
dotnet build FluNET.sln
```

Canonical one-line command:

```bash
dotnet run --project src/FluNET.Cli -- -- "SAY 'Hello from FluNET'."
```

Compact file `hello.fln`:

```text
SAY "Hello from compact FluNET"
```

Run it:

```bash
dotnet run --project src/FluNET.Cli -- run hello.fln
```

Compile compact source without effects:

```bash
dotnet run --project src/FluNET.Cli -- check hello.fln
```

## What you can do

### 1. Basic commands and files

```text
SAY "Hello"
LOAD settings.json
SAY "Environment: {settings.environment}"
```

Canonical file form remains available:

```text
GET [text] FROM {input.txt} THEN SAVE [text] TO {copy.txt}.
```

### 2. Infer resources and dependencies

```text
LOAD post.json, todo.json
SAY "{post.title} — {todo.title}"
```

Compact GET understands built-in providers for:

```text
GET config.json AS config
GET https://api.example.test/posts/1 AS post
GET env:DATABASE_URL AS database_url
GET secret:github-token AS token
```

Secrets are deny-by-default and are not implicitly convertible to Text.

### 3. Reuse API context

```text
USE https://api.example.test AS api
GET api/posts AS posts
GET api/users AS users
```

or:

```text
FROM https://api.example.test
    GET posts AS posts
    GET users AS users
```

### 4. Build typed data pipelines

```text
GET https://api.example.test/posts AS posts
FILTER userId == 1
SORT BY title
TAKE 10
SELECT id, title
```

Equivalent explicit pipe form:

```text
GET https://api.example.test/posts AS posts | FILTER userId == 1 | SORT BY title | TAKE 10
```

Current data transforms include:

```text
FILTER
SORT BY
TAKE
SELECT
MAP TO { ... }
DEFAULT field TO fallback
GROUP BY
SUM
JOIN ... WITH ... ON ...
MATCH ... TO ...
```

### 5. Iterate with bounded concurrency

```text
GET https://api.example.test/users AS users
FOR EACH user PARALLEL 8
    SAY "User: {user.name}"
```

`FOR EACH` supports output, notifications, messaging, resource, HTTP and portable filesystem actions in
the body; see the compact reference for exact constraints.

### 6. Reuse workflow fragments with TASK

```text
TASK fetch-user id RETURNS Json
    GET https://api.example.test/users/{id} AS user
    RETURN [user]

RUN fetch-user 42 AS user
SAY "{user.name}"
```

TASK expansion is compile-time and hygienically renames task-local aliases.

### 7. Apply reliability policies

```text
POLICY resilient
    RETRY 3
    TIMEOUT 30s
    CONTINUE ON ERROR

WITH resilient
    GET https://api.example.test/posts AS posts
```

### 8. Cache reads and deduplicate mutations

```text
GET https://api.example.test/catalog CACHE 1h AS catalog
```

```text
POST order TO https://api.example.test/orders ONCE BY order.id
```

Default cache/idempotency stores are in-memory; embedding hosts can replace them.

### 9. Compile automations

```text
EVERY 1h
    GET https://api.example.test/status AS status
    SAY "Status: {status.name}"
```

```text
WATCH github.issues
    WHEN opened
        SAY "A new issue was opened"
```

Automation is an embedding API. The scheduler is host-driven (`TickAsync` / `PublishSignalAsync`) and creates no hidden background thread.

### 10. Describe desired state

```text
ENSURE backup.json CONTAINS https://api.example.test/config
REFRESH EVERY 1h
KEEP 7 VERSIONS
NOTIFY ON FAILURE
```

ENSURE is also an experimental embedding API. It compiles to the same ordinary GET/SAVE plan and can use version-retention and failure-notifier hooks.

## CLI

Canonical prompt mode:

```text
flunet [options] -- "PROMPT"
```

Compact file tools:

```text
flunet check FILE
flunet fmt FILE
flunet explain FILE
flunet graph FILE
flunet run FILE
```

The short launcher is available as a separate .NET tool command and shares
the same runner:

```text
flu run program.flu
```

For a local developer install, run `scripts/install-flu.ps1` on PowerShell or
`scripts/install-flu.sh` on bash. These scripts pack the project and install
the local `0.3.0-preview` package; the package is not published to NuGet.org.
Alternatively, run it directly from this repository with
`dotnet run --project src/FluNET.Flu -- run program.flu`.

The manual equivalent is:

```text
dotnet pack src/FluNET.Flu/FluNET.Flu.csproj -c Release -o .artifacts/flu-packages
dotnet tool install --global FluNET.Flu --add-source .artifacts/flu-packages --version 0.3.0-preview --ignore-failed-sources
```

The `--global` flag matters: without it, `dotnet tool install` expects a tool
manifest in the current directory or one of its parents.

Capability discovery:

```text
flunet tools
flunet tools --json
```

`tools` reports the capability contracts visible to the current host,
including platform support, permissions and policy availability.

Examples through `dotnet run`:

```bash
dotnet run --project src/FluNET.Cli -- check program.fln
dotnet run --project src/FluNET.Cli -- explain program.fln
dotnet run --project src/FluNET.Cli -- graph program.fln
dotnet run --project src/FluNET.Cli -- run program.fln
```

Canonical CLI file access defaults to the current directory. `--root PATH` can be repeated. If no `--host` is provided, the current canonical CLI leaves network access unrestricted; adding one or more `--host` values restricts network access to those hosts. `--store PATH` enables durable key-value storage for compact `STORE`/`READ`/`LIST STORE`/`DELETE STORE` commands: `.json` uses the atomic JSON backend, while `.db`/`.sqlite` uses SQLite. `--queue PATH` enables durable JSONL messaging for compact `PUBLISH`/`RECEIVE` when passed to `flunet run FILE`, and `--sqlite PATH` enables SQL access. `--config-prefix PREFIX` exposes environment-backed `GETCONFIG` values; `--config PATH` reads nested values from JSON; `--secret-prefix PREFIX` plus repeated `--allow-secret NAME` enables explicitly allow-listed environment secrets.

The compact file-tool path currently uses the current directory as its file root and open network access; it does not expose the canonical runner's `--root`/`--host` parsing for those subcommands yet.

## Architecture in one diagram

```text
canonical source -> SourceDocument -> Lexer             +
                                     -> Sentence[]       |
                                                        |
compact source -> SurfaceParser                         |
                 -> TASK / policies / cache / once       |
                 -> inference + lowering ----------------+
                                                        v
                                                  ProgramSyntax
                                                       |
                                    Bind -> Validate -> Compile
                                                       |
                                                   TypeCheck
                                                       |
                                              DependencyGraph
                                                       |
                                                ExecutionPlan
                                                       |
                                                   Executor
                                                       |
                                                typed handlers
```

Compact/data/automation features do not get separate executors. They compile to the same typed runtime.

## Documentation

Start with [docs/README.md](docs/README.md).

- [Getting started](docs/getting-started.md)
- [Compact language reference](docs/compact-language.md)
- [Canonical language reference](docs/canonical-language.md)
- [Automation and desired state](docs/automation-and-desired-state.md)
- [Embedding and extensibility](docs/embedding-and-extensibility.md)
- [Architecture](docs/architecture.md)
- [Status and limitations](docs/status-and-limitations.md)
- [Durable workflows](docs/durable-workflows.md)

## Important current limitations

The current source tree does **not** provide a released/general implementation for every roadmap idea. In particular:

- compact file inference recognizes CSV/XML/binary/image formats; built-in local decoders cover JSON/text/CSV/XML (plus JSON globs);
- compact HTTP GET currently has a JSON contract;
- generic compact `AUTH`, policy `BACKOFF` and status-specific `CONTINUE ON 404` are not implemented;
- SQL queries use the provider-neutral boundary; the built-in SQLite adapter is opt-in (`--sqlite PATH` in the CLI);
- automation/ENSURE are embedding APIs, not normal `flunet run` commands;
- generic SYNC/reconciliation and distributed workflow coordination are not established public features in the current `main` tree.

See [status and limitations](docs/status-and-limitations.md) for the full support matrix.

## Extensibility

Native modules can register:

- typed commands/binders/handlers with stable `FrameId`;
- language types;
- codecs and explicit conversions;
- resource providers with capability categories.

```csharp
module.Language.Type<Slug>("Slug");
module.Codec<Slug, SlugCodec>();
module.Conversion<Slug, string, SlugToTextConversion>();
module.ResourceProvider<MyResourceProvider>();
```

See [embedding and extensibility](docs/embedding-and-extensibility.md).

## Durability

Workflow execution uses `IWorkflowStateStore`. The default is in-memory; a checksummed, write-through `DurableWorkflowStateStore` is available for single-host restart/resume scenarios:

```csharp
services.AddDurableFluNetWorkflows(".flunet/workflows");
```

See [durable workflows](docs/durable-workflows.md).

## Verification gate

Before changing the public language version or claiming a release from this source tree, run:

```bash
dotnet restore FluNET.sln
dotnet build FluNET.sln --configuration Release --no-restore
dotnet test FluNET.sln --configuration Release --no-build
```

This documentation update does not claim that those commands have passed for the exact current tree.

Author: Paweł Potępa. Licensed under the [MIT License](LICENSE).
