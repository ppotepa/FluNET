# FluNET

[![CI](https://github.com/ppotepa/FluNET/actions/workflows/ci.yml/badge.svg)](https://github.com/ppotepa/FluNET/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)

FluNET is an experimental external DSL and execution engine for small,
English-like automation commands. It is a proof of concept, not a sandbox or a
general-purpose language. The current focus is predictable parsing, typed
semantic binding, explicit capabilities, and useful diagnostics.

```text
GET [text] FROM {input.txt} THEN SAVE [text] TO {copy.txt}.
SAY "Hello from FluNET!"
DOWNLOAD [file] FROM {https://example.com/file.txt} TO {file.txt}.
SET BOOLEAN [enabled] TO true THEN SAY enabled IF [enabled] ELSE SAY disabled.
```

## Quick start

Requirements: .NET 8 SDK.

```bash
git clone https://github.com/ppotepa/FluNET.git
cd FluNET
dotnet build FluNET.sln
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
| `THEN` | Starts the next dependency stage after the preceding stage finishes. |
| `AND` | Adds an independent command to the current stage; ready branches run concurrently. |
| `IF value ... ELSE ...` | Selects one of two command branches. Variables, booleans, numbers, and text can be conditions. |
| `WITH RETRY {n}` | Retries a failed command up to `n` additional times. |
| `WITH TIMEOUT {duration}` | Applies a cooperative timeout such as `250ms`, `5s`, `2m`, or `1h`. |
| `ON ERROR CONTINUE` | Records a failed step and allows the remaining graph to continue. The default is fail-fast. |
| `.`, `?`, `!` | Optional terminators. Attached terminators are tokenized separately. |

Implemented verb families include `GET`, `SAVE`, `LOAD`, `DELETE`, `DOWNLOAD`,
`POST`, `SAY`, `SEND`, `TRANSFORM`, and typed `SET`. `PARSE JSON` and
`FORMAT JSON` provide structured JSON boundaries. Some commands have synonyms
such as `FETCH`, `PULL`, and `ECHO`.

Examples:

```text
SET JSON [config] TO {"enabled":true} THEN FORMAT JSON [pretty] FROM [config].
SAY primary WITH RETRY {2} WITH TIMEOUT {5s} ON ERROR CONTINUE THEN SAY finished.
GET [left] FROM {a.txt} AND GET [right] FROM {b.txt} THEN SAY [left] [right].
```

## Type system

`TypeSymbol` is a FluNET language type, not a CLR type alias. Every symbol has a
stable `TypeId`, `TypeKind`, and nullability. CLR types are runtime mappings only;
multiple CLR representations can map to the same language type. For example,
numeric CLR types map to `Number`, and `string[]` and `List<string>` both map to
the same canonical `List<Text>` symbol.

Built-in language types include `Unit`, `Text`, `Boolean`, `Number`, `File`,
`Directory`, `Uri`, `Json`, and `Object`. `LanguageTypeSystem` also interns
structural `List<T>`, `Map<K,V>`, `Optional<T>`, union, and object-field types:

```csharp
LanguageTypeSystem types = StandardLanguage.CreateSnapshot().Types;
TypeSymbol files = types.List(types.File);
TypeSymbol metadata = types.Map(types.Text, types.Json);
TypeSymbol maybeFile = types.Optional(types.File);
TypeSymbol scalar = types.Union(types.Text, types.Number, types.Boolean);
TypeSymbol person = types.ObjectType(
    new TypeId("example.person"),
    "Person",
    [
        new TypeFieldSymbol("name", types.Text),
        new TypeFieldSymbol("age", types.Number, isRequired: false)
    ]);
```

A module can continue to register a domain CLR mapping with
`LanguageBuilder.Type<T>("LanguageName")`; its `TypeId` is derived from the
language name rather than the CLR class name. Undeclared custom CLR types remain
supported through a `legacy.clr.*` compatibility identity, but published module
APIs should declare their domain types explicitly. Assignability is evaluated
from language structure. The historical conversion of arbitrary values to
`Text` remains temporarily compatible and will move to the explicit conversion
registry in the next compiler batch.

## Embedding

```csharp
using FluNET.Compilation;
using FluNET.Context;
using FluNET.Execution;
using FluNET.Prompt;

using FluNETContext context = FluNETContext.Create();
Engine engine = context.GetEngine();

CompilationResult analysis = engine.Analyze(
    new ProcessedPrompt("GET [text] FROM {input.txt}."));

ExecutionResult execution = await engine.ExecuteAsync(
    new ProcessedPrompt("SAY 'Hello'."),
    cancellationToken);

if (!execution.IsSuccess)
{
    Console.Error.WriteLine($"{execution.Error!.Code}: {execution.Error.Message}");
}
```

Every execution has a journaled workflow run. Supply a stable identifier to
resume only the steps that did not already reach a terminal state:

```csharp
using FluNET.Execution.Workflow;

Guid runId = Guid.Parse("1d98aa21-e92e-41da-9807-98fe5153ad61");
ExecutionResult first = await engine.ExecuteAsync(
    new ProcessedPrompt("GET [text] FROM {input.txt} THEN SAY [text]."),
    new WorkflowExecutionOptions(runId));

ExecutionResult resumed = await engine.ExecuteAsync(
    new ProcessedPrompt("GET [text] FROM {input.txt} THEN SAY [text]."),
    new WorkflowExecutionOptions(runId, Resume: true));
```

The default `InMemoryWorkflowStateStore` supports retries within one host
process. Register `JsonFileWorkflowStateStore` (or an application-specific
`IWorkflowStateStore`) to resume after a process restart. A plan fingerprint
prevents a run identifier from being resumed with changed commands. Extensions
with non-JSON result types can replace `IWorkflowValueSerializer`.

`Analyze` is side-effect free and returns the canonical `CompilationResult`.
`ExecuteAsync` returns structured syntax, validation, activation, capability,
cancellation, or execution errors; an operation failure is never represented
as a successful validation plus an unexplained `null`.

Hosts can replace `IFluNetFileSystem`, `IHttpTransport`, `ITextOutput`,
`IEmailTransport`, and `IExecutionPolicy` through
`FluNETContext.Create(services => ...)`. The CLI uses
`RestrictedExecutionPolicy`; the embedding API keeps `AllowAllExecutionPolicy`
as a backward-compatible default, so production hosts should replace it.

## Architecture

1. `ProcessedPrompt` performs quote-aware lexical analysis and emits stable
   diagnostics (`FLN001` and later) with source positions.
2. `PromptSyntax` is the canonical syntax tree for commands, language-defined
   clause markers, connectors, and execution modifiers.
3. An immutable `LanguageSnapshot` is the single definition of constructions,
   stable `CommandId`/`FrameId`/`TypeId` values, aliases, typed frames,
   extensible frame roles, structural language types, and keywords.
4. `SemanticCommandBinder` selects one frame and labels its arguments by role.
   Prepositions are frame-sensitive, so `FROM` is a marker in `GET` but remains
   message text in `SEND Hello from FluNET TO ...`.
5. `SemanticProgramValidator` validates the selected frames and slots before
   planning. It does not use `WordFactory`, `IWord`, or `SentenceValidator`.
6. Every argument is compiled to a typed `IExpression<T>` tree (literal,
   variable, list, conversion, JSON, or extension-defined expression).
7. `ExecutionPlanner` turns the bound program into a typed DAG with explicit
   sequence, parallel-stage, condition, variable-flow, and result-storage edges.
8. `ExecutionPlanExecutor` schedules ready nodes concurrently, persists
   append-only workflow events, restores completed outputs on resume, and
   applies retry, timeout, condition, and error policies.
9. Each node dispatches through generic `ICommandHandler<TCommand, TResult>`
   routes selected by stable `FrameId`. Typed dispatch and effect execution do
   not use the legacy word-chain.

The standard execution path is:

```text
Parse -> Bind -> Validate -> Plan -> Execute
```

`ISentence`, `SentenceValidator`, old verb objects, and token trees are retained
only behind `LegacySentenceAdapter` for source compatibility. `Engine.Run` and
`ISentence` are deprecated; new code should use `Analyze`, `CompilationResult`,
and `ExecuteAsync`. See [the 0.3 migration guide](docs/legacy-api-migration.md).

Language extensions are explicit modules rather than an ambient scan of all
loaded assemblies. Native modules declare a typed command, semantic frame,
binder, and handler without inheriting from `IVerb` or `IWord`:

```csharp
public sealed class ReportingModule : IFluNetModule
{
    public void Register(FluNetModuleBuilder module)
    {
        module.Language
            .Module("reporting")
            .CommandConnector("AFTER", CommandLinkKind.Sequence);

        module.Command<GenerateReportCommand, FileInfo>("GENERATE", "Report")
            .FrameId("reporting.generate")
            .Aliases("BUILD")
            .Qualifiers("REPORT")
            .Positional<FileInfo>(SemanticRole.Output, SlotDirection.Output)
            .Marked<DateOnly>(SemanticRole.Source, "FROM")
            .BindWith<GenerateReportBinder>()
            .HandleWith<GenerateReportHandler>();
    }
}

FluNetRuntimeDefinition runtime = new FluNetModuleBuilder()
    .AddModule(new StandardLanguageModule())
    .AddModule(new ReportingModule())
    .Build();

using FluNETContext context = FluNETContext.CreateWithRuntime(runtime);
```

The runtime validates command, synonym, keyword, type, frame, and typed-handler
route collisions atomically before the host starts. Custom `FrameRoleId` and
`TypeSymbol` values let extensions add domain roles and types without changing
engine enums. Handlers may run concurrently when their DAG nodes are ready, so
extension-owned mutable state must be synchronized.
Add executable examples and tests for every frame's grammar, activation,
success path, and failure path.

## Tests

```bash
dotnet test FluNET.sln --configuration Release
```

HTTP behavior is tested with an injected in-memory transport; no manual test
server or public network connection is required. CI builds and tests on Linux,
Windows, and macOS.

## Status and limitations

- The grammar is intentionally controlled rather than an attempt at unrestricted
  natural-language understanding. New surface forms belong in language modules.
- Every built-in frame executes through a typed command and handler. The old
  word-chain remains only as a deprecated compatibility projection.
- The default `IEmailTransport` is diagnostic-only; production hosts should
  inject an SMTP or API-backed implementation.
- This project does not make untrusted prompts safe by itself. Configure a
  restrictive execution policy and isolate the host process where appropriate.
- JSON-lines workflow persistence targets a single host. Distributed execution
  needs a transactional `IWorkflowStateStore` with cross-process coordination.

Author: Paweł Potępa. Licensed under the [MIT License](LICENSE).
