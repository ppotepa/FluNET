# FluNET

[![CI](https://github.com/ppotepa/FluNET/actions/workflows/ci.yml/badge.svg)](https://github.com/ppotepa/FluNET/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)

FluNET is an experimental external DSL and execution engine for small,
English-like automation commands. It is a proof of concept, not a sandbox or a
general-purpose language. The current compiler work focuses on predictable
parsing, typed semantic binding, explicit value conversions, typed variables,
side-effect-free analysis, and useful diagnostics.

```text
GET [text] FROM {input.txt} THEN SAVE [text] TO {copy.txt}.
SAY "Hello from FluNET!"
DOWNLOAD [file] FROM {https://example.com/file.txt} TO {file.txt}.
SET BOOLEAN [enabled] TO true THEN SAY enabled IF [enabled] ELSE SAY disabled.
SAY ready IF ([enabled] AND NOT [blocked]).
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
| `AND` | Adds an independent command to the current stage; ready branches run concurrently. Inside parenthesized `IF (...)` it is a Boolean operator. |
| `IF expression ... ELSE ...` | Selects one of two command branches from a compiled expression. |
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
SET NUMBER [count] TO 3 THEN SAY positive IF ([count] > 0).
```

Condition expressions have explicit precedence for `OR`, `AND`, equality,
ordering, arithmetic, unary operators and postfix access. The expression AST
also supports `LIST(...)`, `OBJECT(...)`, property access and indexing.
Parentheses are recommended whenever `AND`/`OR` appears in a command condition,
so the boundary between command links and Boolean operators remains explicit.

## Type and value system

`TypeSymbol` is a FluNET language type, not a CLR type alias. Every symbol has a
stable `TypeId`, `TypeKind`, and nullability. CLR types are runtime mappings only;
multiple CLR representations can map to the same language type. For example,
numeric CLR types map to `Number`, and `string[]` and `List<string>` both map to
the same canonical `List<Text>` symbol.

Built-in language types include `Unit`, `Text`, `Boolean`, `Number`, `File`,
`Directory`, `Uri`, `Json`, and `Object`. `LanguageTypeSystem` also interns
structural `List<T>`, `Map<K,V>`, `Optional<T>`, unions, and object-field types:

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

`TypeSymbol.IsAssignableFrom` is purely structural. Text/Number/File/Uri/Json and
other boundary conversions are explicit edges in `IValueCodecRegistry`; the
runtime does not use arbitrary CLR `ToString()` as a canonical conversion.
The standard language also declares `List<Text> -> Text` so line-producing
commands such as `GET` and `LOAD` can feed a text consumer such as `SAY`.

A module can register a stable domain type and its value boundary explicitly:

```csharp
module.Language.Type<Slug>("Slug");
module.Codec<Slug, SlugCodec>();
module.Conversion<Slug, string, SlugToTextConversion>();
```

Undeclared CLR types remain available only through compatibility identities;
published module contracts should declare their language types and codecs.

## Embedding

```csharp
using FluNET.Compilation;
using FluNET.Context;
using FluNET.Execution;
using FluNET.Prompt;

using FluNETContext context = FluNETContext.Create();
Engine engine = context.GetEngine();

// Source-compatible parse/bind/semantic/plan analysis.
CompilationResult analysis = engine.Analyze(
    new ProcessedPrompt("GET [text] FROM {input.txt}."));

// 0.4 freeze-candidate typed analysis: compile + type-check, no handlers run.
TypedAnalysisResult typed = context.AnalyzeTyped(
    new ProcessedPrompt("SET NUMBER [count] TO 42 THEN SAY [count]."));

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

Hosts can replace `IFluNetFileSystem`, `IHttpTransport`, `ITextOutput`,
`IEmailTransport`, and `IExecutionPolicy` through
`FluNETContext.Create(services => ...)`. The CLI uses
`RestrictedExecutionPolicy`; the embedding API keeps `AllowAllExecutionPolicy`
as a backward-compatible default, so production hosts should replace it.

## Architecture

1. `ProcessedPrompt` performs quote-aware lexical analysis and emits stable
   diagnostics (`FLN001` and later) with source positions.
2. `PromptSyntax` is the command tree for language-defined clauses, connectors,
   and execution modifiers. Parenthesis depth protects command connectors inside
   condition expressions.
3. `ExpressionSyntaxParser` builds a separate expression AST with Boolean,
   comparison, arithmetic, collection and access nodes.
4. An immutable `LanguageSnapshot` is the definition of stable
   `CommandId`/`FrameId`/`TypeId` values, aliases, typed frames, extensible roles,
   structural types and keywords.
5. `SemanticCommandBinder` selects a frame and labels its arguments by role;
   `SemanticProgramValidator` checks the selected frame/slots.
6. `ExpressionBinder` and `CommandBindingContext` compile bound arguments to
   typed `IExpression<T>` trees through `IValueCodecRegistry`.
7. `TypedProgramCompiler` binds each module command once to `CompiledCommand`.
8. `TypedProgramTypeValidator` checks producer/consumer flow, host variables,
   implicit conversions, condition dependencies and parallel-write conflicts.
9. `ExecutionPlanner` builds only the orchestration DAG: sequence/data edges,
   policies and result bindings.
10. `ExecutionPlanExecutor` schedules ready nodes, evaluates cached typed
    conditions, persists workflow events and dispatches typed handlers by
    stable `FrameId`.

The canonical standard execution path is:

```text
Parse -> Bind -> Validate -> Compile -> TypeCheck -> Plan -> Execute
```

`ISentence`, `SentenceValidator`, old verb objects, token trees and historical
scalar/text/JSON expression helpers remain only as compatibility APIs. Module
execution uses the typed compiler path; direct-DI typed route registration is
retained for 0.3 source compatibility.

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

The runtime validates command, synonym, keyword, type, frame, typed-handler,
codec and conversion registrations before the host starts. Handlers may run
concurrently when their DAG nodes are ready, so extension-owned mutable state
must be synchronized.

## Tests

Release verification for the compiler freeze candidate is:

```bash
dotnet restore FluNET.sln
dotnet build FluNET.sln --configuration Release --no-restore
dotnet test FluNET.sln --configuration Release --no-build
```

HTTP behavior is tested with an injected in-memory transport; no manual test
server or public network connection is required. CI is configured for Linux,
Windows, and macOS.

## Status and limitations

- The source tree is a **0.4 compiler freeze candidate**, but the published
  `StandardLanguageIdentity.Version` intentionally remains `0.3` until a real
  Release build/test run verifies the exact tree.
- The grammar is intentionally controlled rather than an attempt at unrestricted
  natural-language understanding. New surface forms belong in language modules.
- Every built-in frame executes through a typed command and handler. The old
  word-chain remains only as a deprecated compatibility projection.
- `Engine.Analyze` remains source-compatible; use `FluNETContext.AnalyzeTyped`
  when typed compilation/type-check validity is required without execution.
- The default `IEmailTransport` is diagnostic-only; production hosts should
  inject an SMTP or API-backed implementation.
- This project does not make untrusted prompts safe by itself. Configure a
  restrictive execution policy and isolate the host process where appropriate.
- JSON-lines workflow persistence targets a single host. Distributed execution
  needs a transactional `IWorkflowStateStore` with cross-process coordination.

See [compiler 0.4 freeze readiness](docs/compiler-0.4-freeze-readiness.md) for
the release gate. Author: Paweł Potępa. Licensed under the [MIT License](LICENSE).
