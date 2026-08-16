# FluNET

[![CI](https://github.com/ppotepa/FluNET/actions/workflows/ci.yml/badge.svg)](https://github.com/ppotepa/FluNET/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)

<<<<<<< HEAD
FluNET is an experimental .NET DSL/compiler/runtime for readable local automation and typed data workflows. It supports an explicit **canonical syntax** and a higher-level **compact syntax** that can infer obvious resource types, output names and data dependencies before lowering to the same typed execution engine.

```text
LOAD post.json, todo.json
SAY "{post.title} — {todo.title}"
```

The compiler can infer that the two reads are independent, schedule them together and make `SAY` depend on both outputs. You can still use explicit canonical `AND`/`THEN` when you want exact control edges.

> **Project status:** the public built-in `LanguageVersion` is still `0.3`. `main` contains source-level compact/data/automation work beyond that published version, but the exact tree has not yet been release-verified with a successful Release restore/build/test run. Treat advanced surfaces as experimental until that gate is complete.

## Quick start

Requirement: .NET 8 SDK.
=======
FluNET is an experimental external DSL and execution engine for small,
English-like automation commands. It is a proof of concept, not a sandbox or a
general-purpose language. The current focus is predictable parsing, typed verb
activation, explicit capabilities, and useful diagnostics.

```text
GET [text] FROM {input.txt} THEN SAVE [text] TO {copy.txt}.
SAY "Hello from FluNET!"
DOWNLOAD [file] FROM {https://example.com/file.txt} TO {file.txt}.
```

## Quick start

Requirements: .NET 8 SDK.
>>>>>>> origin/agent/stabilize-poc-foundation

```bash
git clone https://github.com/ppotepa/FluNET.git
cd FluNET
dotnet build FluNET.sln
<<<<<<< HEAD
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

Current `FOR EACH` is intentionally limited to `SAY` actions in the body. See the compact reference for exact constraints.

### 6. Reuse workflow fragments with TASK

```text
TASK fetch-user id -> Json
    GET https://api.example.test/users/{id} AS user

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

Examples through `dotnet run`:

```bash
dotnet run --project src/FluNET.Cli -- check program.fln
dotnet run --project src/FluNET.Cli -- explain program.fln
dotnet run --project src/FluNET.Cli -- graph program.fln
dotnet run --project src/FluNET.Cli -- run program.fln
```

Canonical CLI file access defaults to the current directory. `--root PATH` can be repeated. If no `--host` is provided, the current canonical CLI leaves network access unrestricted; adding one or more `--host` values restricts network access to those hosts.

The compact file-tool path currently uses the current directory as its file root and open network access; it does not expose the canonical runner's `--root`/`--host` parsing for those subcommands yet.

## Architecture in one diagram

```text
canonical source -> ProcessedPrompt ------------------+
                                                       |
compact source -> SurfaceParser                       |
                 -> TASK / policies / cache / once    |
                 -> inference + lowering --------------+
                                                       v
                                                 PromptSyntax
                                                       |
                                    Bind -> Validate -> Compile
                                                       |
                                                   TypeCheck
                                                       |
                                              DependencyGraph
                                                       |
                                                ExecutionPlan
                                                       |
                                            ExecutionPlanExecutor
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
- [Legacy API migration](docs/legacy-api-migration.md)

## Important current limitations

The current source tree does **not** provide a released/general implementation for every roadmap idea. In particular:

- compact file inference recognizes CSV/XML/binary/image formats, but built-in compact decoders currently cover JSON/text (plus JSON globs);
- compact HTTP GET currently has a JSON contract;
- `FOR EACH` body actions are currently SAY-only;
- generic compact `AUTH`, policy `BACKOFF` and status-specific `CONTINUE ON 404` are not implemented;
- SQL has no built-in executable resource provider;
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
=======
dotnet run --project src/FluNET.Cli -- -- "SAY 'Hello from FluNET'."
```

Validate without executing:

```bash
dotnet run --project src/FluNET.Cli -- --analyze -- "GET [text] FROM {input.txt}"
```

The CLI restricts file access to the current directory by default and denies
network access by default. Grant only the capabilities a command needs:

```bash
dotnet run --project src/FluNET.Cli -- \
  --root ./downloads \
  --host example.com \
  -- "DOWNLOAD [file] FROM {https://example.com/file.txt} TO {./downloads/file.txt}."
```

Use `--root` and `--host` more than once to allow multiple roots or hosts.

## Language surface

| Form | Meaning |
| --- | --- |
| `[name]` | A named variable. Retrieval verbs write results to it; other verbs read it. |
| `{value}` | An inline reference such as a path, URL, or JSON object. Spaces are preserved. |
| `"text"` or `'text'` | A quoted literal. Spaces, newlines, and escaped quotes are preserved. |
| `THEN` | Runs another command with the same variable context. |
| `.`, `?`, `!` | Optional terminators. Attached terminators are tokenized separately. |

Implemented verb families include `GET`, `SAVE`, `LOAD`, `DELETE`, `DOWNLOAD`,
`POST`, `SAY`, `SEND`, and `TRANSFORM`. Some have synonyms such as `FETCH`,
`PULL`, and `ECHO`.

## Embedding

```csharp
using FluNET.Context;
using FluNET.Execution;
using FluNET.Prompt;

using FluNETContext context = FluNETContext.Create();
Engine engine = context.GetEngine();

PromptAnalysis analysis = engine.Analyze(
    new ProcessedPrompt("GET [text] FROM {input.txt}."));

ExecutionResult execution = await engine.ExecuteAsync(
    new ProcessedPrompt("SAY 'Hello'."),
    cancellationToken);

if (!execution.IsSuccess)
{
    Console.Error.WriteLine($"{execution.Error!.Code}: {execution.Error.Message}");
}
```

`Analyze` is side-effect free. `ExecuteAsync` returns structured syntax,
validation, activation, capability, cancellation, or execution errors; an
operation failure is never represented as a successful validation plus an
unexplained `null`.

Hosts can replace `IFluNetFileSystem`, `IHttpTransport`, `ITextOutput`, and
`IExecutionPolicy` through `FluNETContext.Create(services => ...)`. The CLI uses
`RestrictedExecutionPolicy`; the embedding API keeps `AllowAllExecutionPolicy`
as a backward-compatible default, so production hosts should replace it.

## Architecture

1. `ProcessedPrompt` performs quote-aware lexical analysis and emits stable
   diagnostics (`FLN001` and later) with source positions.
2. `PromptSyntax` represents the top-level command sequence; the older linked
   token/word chain remains an execution compatibility layer.
3. `LanguageRegistry` performs one deterministic, sorted discovery pass and
   rejects ambiguous names or synonyms.
4. `SentenceValidator` validates every command, including commands after
   `THEN`, before execution starts.
5. The asynchronous execution pipeline activates typed verbs through one
   registry and passes cancellation into injected effect capabilities.

To add a verb, implement the appropriate `IVerb`/noun interfaces and ensure its
assembly is loaded before creating `FluNETContext`. Add executable examples and
tests for its grammar, activation, success path, and failure path. Name and
synonym collisions fail registry construction with a clear error.

## Tests

```bash
dotnet test FluNET.sln --configuration Release
```

HTTP behavior is tested with an injected in-memory transport; no manual test
server or public network connection is required. CI builds and tests on Linux,
Windows, and macOS.

## Status and limitations

- The grammar is intentionally small and still evolving.
- The typed syntax model currently covers command boundaries; verb arguments
  still flow through the compatibility word chain.
- `SEND` is a simulated implementation rather than a real mail transport.
- This project does not make untrusted prompts safe by itself. Configure a
  restrictive execution policy and isolate the host process where appropriate.

Author: Paweł Potępa. Licensed under the [MIT License](LICENSE).
>>>>>>> origin/agent/stabilize-poc-foundation
