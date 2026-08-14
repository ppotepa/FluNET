# FluNET documentation

This directory documents the source tree on `main`. FluNET currently has two authoring surfaces that converge on one compiler/runtime:

- **canonical syntax** — explicit low-level commands such as `GET [text] FROM {input.txt} THEN SAY [text]`;
- **compact syntax** — inference-oriented source such as `LOAD post.json, todo.json` followed by `SAY "{post.title} — {todo.title}"`.

The public built-in `LanguageVersion` is still `0.3`. The repository also contains source-level compiler/data/automation work beyond that published version, but the exact tree has not yet been release-verified with a successful Release restore/build/test run. Treat advanced surfaces as experimental until that gate is complete.

## Start here

1. [Getting started](getting-started.md) — a progressive tour from `SAY` through files, HTTP, inferred dependencies, data pipelines, reusable tasks, automation and ENSURE.
2. [Compact language reference](compact-language.md) — the current compact syntax and its exact limitations.
3. [Canonical language reference](canonical-language.md) — explicit command syntax, variables, connectors, conditions and execution policies.
4. [Automation and desired state](automation-and-desired-state.md) — EVERY/WATCH/WHEN, scheduler APIs, durable timer state and ENSURE.
5. [Embedding and extensibility](embedding-and-extensibility.md) — C# APIs, modules, codecs, conversions, resource providers, secrets and durable stores.
6. [Architecture](architecture.md) — front ends, compiler passes, typed IR, dependency graph and the single execution runtime.
7. [Status and limitations](status-and-limitations.md) — support matrix, current gaps and the release verification gate.
8. [Durable workflows](durable-workflows.md) — workflow journals, resume semantics and durable single-host storage.

## Historical/milestone notes

The following files record source-level milestone readiness. They are not a replacement for the current user documentation above:

- [0.4 compiler freeze readiness](compiler-0.4-freeze-readiness.md)
- [0.6 data-language freeze readiness](compiler-0.6-freeze-readiness.md)
- [0.7 automation-language freeze readiness](compiler-0.7-freeze-readiness.md)
- [legacy API migration](legacy-api-migration.md)
- [compiler preview contract](contracts/compiler-0.4-preview.json)

## One architectural rule

Compact syntax, canonical syntax, data operations, TASK expansion and automation workflow bodies all converge on the same typed compilation and `ExecutionPlanExecutor`. There is no separate compact/query/automation executor.