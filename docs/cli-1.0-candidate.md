# FluNET 1.0 candidate CLI

`src/FluNET.Tool` is the complete 1.0-source-candidate command surface. It is additive: the existing `FluNET.Cli` remains the compatibility entry point until the 1.0 candidate is build/test verified.

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

Until verification, build it explicitly with:

```bash
dotnet build src/FluNET.Tool/FluNET.Tool.csproj --configuration Release
```
