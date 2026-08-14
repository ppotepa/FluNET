# FluNET 1.0 candidate CLI

`src/FluNET.Tool` is the preferred source-candidate command surface. It now targets the same `net9.0` framework as `FluNET.Engine`, is included in `FluNET.sln`, and is configured as the `FluNET.Tool` .NET tool package with command name `flunet`.

The package version deliberately remains **`0.3.0-preview`** while public `StandardLanguageIdentity.Version` is `0.3`. A production package/version promotion is part of the final verified release gate, not this source batch.

The historical `src/FluNET.Cli` project remains buildable for compatibility but is explicitly `IsPackable=false`; new CLI work belongs in `FluNET.Tool`.

Commands:

```text
flunet version
flunet contract
flunet exec "CANONICAL PROMPT"

flunet check FILE
flunet fmt FILE
flunet explain FILE
flunet graph FILE
flunet run FILE
flunet capabilities FILE

flunet automation check FILE
flunet automation run FILE
flunet automation tick FILE ...
flunet automation signal FILE RESOURCE [EVENT]

flunet ensure check FILE
flunet ensure apply FILE

flunet sync check FILE
flunet sync apply FILE

flunet history list DIRECTORY
flunet history show DIRECTORY RUN_ID
flunet persistence
```

The tool calls the same compiler/runner APIs used by embedding hosts. It contains no second implementation of language, automation, ENSURE or reconciliation semantics.

The canonical release scripts now build the solution, run tests and then pack/install the local `FluNET.Tool` package into a temporary tool directory for a smoke check.
