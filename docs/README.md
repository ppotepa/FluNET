# FluNET documentation

FluNET has an explicit canonical surface and an inference-oriented compact surface; both converge on the same typed compiler/runtime. Public built-in `LanguageVersion` remains `0.3` until the exact tree passes Release verification.

## Start here

1. [Getting started](getting-started.md)
2. [Compact language reference](compact-language.md)
3. [Statement separators](statement-separators.md)
4. [Canonical language reference](canonical-language.md)
5. [Automation and desired state](automation-and-desired-state.md)
6. [Embedding and extensibility](embedding-and-extensibility.md)
7. [Architecture](architecture.md)
8. [Ecosystem tree](ecosystem-tree.md)
9. [Status and limitations](status-and-limitations.md)
10. [Durable workflows](durable-workflows.md)
11. [Master roadmap](roadmap.md)
12. [1.0 RC source readiness](1.0-rc-readiness.md)
13. [1.0 verification gate](1.0-verification.md)
14. [.NET API 1.0 candidate boundary](contracts/dotnet-api-1.0-candidate.md)

## Milestone ledgers

- [0.4 compiler](compiler-0.4-freeze-readiness.md)
- [0.5 compact language](compiler-0.5-freeze-readiness.md)
- [0.6 data language](compiler-0.6-freeze-readiness.md)
- [0.7 automation language](compiler-0.7-freeze-readiness.md)
- [0.8 integration/execution](compiler-0.8-freeze-readiness.md)
- [0.9 reconciliation](compiler-0.9-freeze-readiness.md)
- [1.0 RC source readiness](1.0-rc-readiness.md)

Compact, canonical, data, task, automation, ENSURE and reconciliation mutation execution reuse the typed command/planning/execution stack; there is no second query/automation/reconciliation command executor.
