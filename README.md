# FluNET

[![CI](https://github.com/ppotepa/FluNET/actions/workflows/ci.yml/badge.svg)](https://github.com/ppotepa/FluNET/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)

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

Hosts can replace `IFluNetFileSystem`, `IHttpTransport`, `ITextOutput`,
`IEmailTransport`, and `IExecutionPolicy` through
`FluNETContext.Create(services => ...)`. The CLI uses
`RestrictedExecutionPolicy`; the embedding API keeps `AllowAllExecutionPolicy`
as a backward-compatible default, so production hosts should replace it.

## Architecture

1. `ProcessedPrompt` performs quote-aware lexical analysis and emits stable
   diagnostics (`FLN001` and later) with source positions.
2. `PromptSyntax` represents the top-level command sequence; the older linked
   token/word chain remains a public compatibility view, not an execution path.
3. An immutable `LanguageSnapshot` is the single definition of commands,
   aliases, typed frames, semantic roles, and keywords. `LanguageRegistry`
   projects the legacy word-chain view from that snapshot.
4. `SemanticCommandBinder` selects one lexical frame and labels its arguments
   by role. Prepositions are frame-sensitive, so `FROM` is a marker in `GET`
   but remains message text in `SEND Hello from FluNET TO ...`.
5. `SentenceValidator` validates every command, including commands after
   `THEN`, before execution starts.
6. `ExecutionPlanner` turns bound commands into immutable steps with explicit
   sequence, variable-flow, and result-storage edges.
7. `ExecutionPlanExecutor` dispatches every step through generic
   `ICommandHandler<TCommand, TResult>` routes. Cancellation flows into injected
   effect capabilities. Typed dispatch and effect execution do not use
   reflection; only projection of the legacy `ISentence` compatibility view may
   instantiate old word types.

Language extensions are explicit modules rather than an ambient scan of all
loaded assemblies. Implement `IFluNetModule`, declare each command frame, and
compose one snapshot at host startup:

```csharp
public sealed class ReportingModule : IFluNetModule
{
    public void Register(LanguageBuilder language) =>
        language.Command<GenerateReport, FileInfo>("GENERATE", "Report")
            .Aliases("BUILD")
            .Qualifiers("REPORT")
            .Positional<FileInfo>(SemanticRole.Output, SlotDirection.Output)
            .Marked<DateOnly>(SemanticRole.Source, "FROM");
}

LanguageSnapshot language = new LanguageBuilder()
    .AddModule(new StandardLanguageModule())
    .AddModule(new ReportingModule())
    .Build();

using FluNETContext context = FluNETContext.Create(services =>
{
    services.AddSingleton(language);
    services.AddTypedCommand<GenerateReportCommand, FileInfo,
        GenerateReportBinder, GenerateReportHandler>();
});
```

The snapshot validates command, synonym, and keyword collisions atomically.
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

- The grammar is intentionally small and still evolving.
- Every built-in frame executes through a typed command and handler. The old
  word-chain executor remains callable only as a compatibility API; extensions
  participate in the standard pipeline by registering a typed route.
- The default `IEmailTransport` is diagnostic-only; production hosts should
  inject an SMTP or API-backed implementation.
- This project does not make untrusted prompts safe by itself. Configure a
  restrictive execution policy and isolate the host process where appropriate.

Author: Paweł Potępa. Licensed under the [MIT License](LICENSE).
