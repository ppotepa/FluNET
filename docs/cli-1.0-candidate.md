# FluNET 1.0 candidate CLI

`src/FluNET.Tool` is the preferred source-candidate command surface. It now targets the same `net9.0` framework as `FluNET.Engine`, is included in `FluNET.sln`, and is configured as the `FluNET.Tool` .NET tool package with command name `flunet`.

The package version deliberately remains **`0.3.0-preview`** while public `StandardLanguageIdentity.Version` is `0.3`. A production package/version promotion is part of the final verified release gate, not this source batch.

`src/FluNET.Cli` is a thin, non-packable project wrapper around the same
`FluNET.Tool` runner. This keeps `dotnet run --project src/FluNET.Cli` and the
packable tool on one command implementation.

For the shortest invocation, `src/FluNET.Flu` provides the separately
packable `flu` command and delegates to the exact same runner:

```text
flu run program.flu
```

Commands:

```text
flunet version
flunet contract
flunet exec "CANONICAL PROMPT"

flunet check FILE
flunet fmt FILE
flunet explain FILE
flunet graph FILE
flunet run FILE [--queue PATH] [--store PATH] [--blob PATH]
flunet run -v FILE [--queue PATH] [--store PATH] [--blob PATH]
flunet run -vv FILE [--queue PATH] [--store PATH] [--blob PATH]
flunet run -vvv FILE [--queue PATH] [--store PATH] [--blob PATH]
flunet capabilities FILE
flunet tools

flunet automation check FILE
flunet automation run FILE
flunet automation tick FILE ...
flunet automation signal FILE RESOURCE [EVENT]
flunet automation watch FILE DIRECTORY RESOURCE [--filter PATTERN] [--recursive]

flunet ensure check FILE
flunet ensure apply FILE

flunet sync check FILE
flunet sync apply FILE

flunet history list DIRECTORY
flunet history show DIRECTORY RUN_ID
flunet persistence

flu run FILE [--queue PATH] [--store PATH] [--blob PATH]
```

With no arguments on an attached terminal, both runners open the interactive
session. Use `:begin` and `:end` for pasted blocks, or `:paste` to execute the
current system clipboard. `:check`, `:dry-run`, `:explain`, `:graph` and `:fmt`
inspect a prompt without executing its effects.

`run` is quiet by default. Add `-v` for completed-step progress, `-vv` for
the execution plan and dependencies, or `-vvv` for step results and full
failure details. The equivalent explicit form is `--verbosity 0..3`.
Verbosity flags may be placed before or after `FILE`, for example
`flu run program.flu -vvv`.

The tool calls the same compiler/runner APIs used by embedding hosts. It contains no second implementation of language, automation, ENSURE or reconciliation semantics.

The canonical release scripts now build the solution, run tests and then pack/install the local `FluNET.Tool` package into a temporary tool directory for a smoke check.

To install the short launcher from this repository, use
`scripts/install-flu.ps1` on PowerShell or `scripts/install-flu.sh` on bash.
This is a local preview package and is not yet published to NuGet.org. For
development, run it without installing anything:

```text
dotnet run --project src/FluNET.Flu -- run program.flu
```
