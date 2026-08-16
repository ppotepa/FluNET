# FluNET architecture

FluNET has multiple authoring surfaces but one canonical typed execution architecture. The central rule is:

> Front ends may infer, expand or lower syntax, but they must converge on the same semantic binding, typed program, dependency graph, execution plan and executor.

There is no separate compact executor, query executor or automation executor.

## High-level data flow

```text
                       +--------------------+
canonical source ----> | ProcessedPrompt    |
                       +---------+----------+
                                 |
                                 v
                            PromptSyntax
                                 ^
                                 |
compact source ------> Surface front end
                                 |
                                 +-- SurfaceParser
                                 +-- TASK expansion
                                 +-- policy profiles
                                 +-- CACHE extraction
                                 +-- ONCE BY extraction
                                 +-- inference/lowering
                                 +-- SourceMap / InferenceTrace
                                 |
                                 v
                            PromptSyntax
                                 |
                                 v
                    SemanticCommandBinder
                                 |
                    SemanticProgramValidator
                                 |
                                 v
                          BoundProgram
                                 |
                                 v
                     TypedProgramCompiler
                                 |
                                 v
                          TypedProgram
                                 |
                                 v
                  TypedProgramTypeValidator
                                 |
                                 v
                      DependencyAnalyzer
                                 |
                                 v
                       DependencyGraph
                                 |
                                 v
                       ExecutionPlanner
                                 |
                                 v
                        ExecutionPlan
                                 |
                                 v
                           SentenceExecutor
                                 |
                                 v
                        typed handlers
```

## Canonical front end

`ProcessedPrompt` preserves the source-level `Sentence[]` produced by `SentenceSegmenter`, then performs quote-aware tokenization and builds `PromptSyntax`. Canonical syntax contains:

- commands;
- clauses/markers;
- connectors such as `AND`/`THEN`/`ELSE`;
- command modifiers such as retry, timeout, condition and error policy.

The front-end boundary is therefore:

```text
SourceDocument / ProcessedPrompt
  -> Lexer
  -> TokenStream
  -> SentenceSegmenter
  -> Sentence[]
  -> SentenceParser / SurfaceParser
  -> ProgramSyntax
```

`Sentence[]` preserves source text, spans, terminators and indentation. The
The execution pipeline has one typed representation: source syntax becomes a
bound program, then an execution plan consumed by `SentenceExecutor`.

Canonical source is the explicit low-level representation consumed by semantic binding.

## Compact/surface front end

`SourceDocument` carries source text plus a requested syntax kind (`Auto`, `Canonical`, `Compact`). Surface parsing itself does no I/O or inference.

The current compact compiler pipeline is:

```text
SourceDocument
  -> SurfaceParser
  -> SurfaceTaskCompiler
  -> SurfacePolicyCompiler
  -> SurfaceCacheCompiler
  -> SurfaceIdempotencyCompiler
  -> SurfaceLowerer
  -> SurfacePolicyApplicationPass
  -> PromptSyntax
```

### SurfaceParser

Builds indentation-aware `SurfaceProgramSyntax` nodes:

- ordinary commands;
- `|` pipelines;
- `FROM` lexical contexts;
- policy definitions/contexts;
- TASK definitions;
- FOR EACH blocks (compiled to an iteration descriptor).

### TASK expansion

`SurfaceTaskCompiler` is a compile-time macro/template layer:

- validates task name/parameters/result type;
- substitutes explicit `{parameter}` placeholders;
- hygienically renames local aliases;
- expands RUN calls;
- rejects recursion/cycles and excessive expansion depth.

No TASK object is executed at runtime. After expansion the body is ordinary surface syntax.

### Policy profiles

`SurfacePolicyCompiler` removes `POLICY` definitions and `WITH profile` contexts from executable syntax. It records effective policy metadata by source span and later lowers it to the existing canonical retry/timeout/error modifiers.

### CACHE / ONCE BY

CACHE and idempotency are extracted before normal lowering and attached later to the corresponding immutable `BoundCommand` as execution artifacts. This keeps them out of GET/POST handler-specific logic.

### Inference and lowering

`SurfaceLowerer` performs deterministic, side-effect-free decisions such as:

- local/HTTP/environment/secret resource classification;
- format/type/name inference;
- named and lexical URI-base resolution;
- compact data-stage lowering;
- interpolation lowering;
- mapping compact mutations to canonical SAVE/POST frames.

Resource reads are delegated to `IResourceProviderRegistry`, not a hard-coded GET switch.

## SourceMap and InferenceTrace

Compact code is not converted to a generated prompt string and reparsed. Lowering constructs canonical syntax nodes directly.

`SourceMap` maps lowered command indices back to original source spans. It is used by policy/artifact passes and is the basis for source-aware diagnostics/tooling.

`InferenceTrace` records decisions such as:

```text
post.json -> LocalFile
post.json -> Json
post.json -> variable post
```

`flunet explain` surfaces those compiler decisions.

## Language snapshot

`LanguageSnapshot` is the immutable language contract for one runtime. It carries:

- stable `CommandId`;
- stable `FrameId`;
- stable `TypeId`;
- module ownership;
- aliases/qualifiers;
- semantic slots/roles;
- grammar metadata;
- structural type system.

Runtime command identity does not depend on CLR class names.

## Semantic binding

`SemanticCommandBinder` selects a command frame and labels arguments by semantic role. `SemanticProgramValidator` checks frame registration, markers, required roles and cardinality-related semantic invariants.

At this stage FluNET knows what each command means, but it has not executed anything.

## Typed command compilation

`TypedProgramCompiler` invokes each typed route binder before planning and creates `CompiledCommand` objects. Argument expressions are created through `ExpressionBinder` and `CommandBindingContext`.

Important invariant:

```text
bad literal / bad conversion
        -> compiler diagnostic
        -> handler never sees the command
```

The executor should consume already-bound typed command values rather than reinterpret user text.

## Type system and values

`TypeSymbol` is language-native. Identity and assignability are not CLR inheritance.

Built-in/constructed concepts include:

```text
Text, Boolean, Number, File, Directory, Uri, Json, Object, Secret
List<T>
Map<K,V>
Optional<T>
Union
structural object fields
```

`LanguageTypeSystem` interns structural types. `IValueCodecRegistry` owns literal parsing, formatting and explicit/implicit conversion paths.

Numeric CLR representations map to the single language-level `Number` model; runtime storage normalizes number representation at its boundary.

Secrets deliberately remain outside Text conversion.

## Typed variable flow

Variables are typed producer/consumer symbols. `TypedProgramTypeValidator` checks:

- missing producers/host variables;
- stage visibility;
- producer/consumer type compatibility;
- conversion availability;
- condition dependencies;
- parallel write conflicts.

The planner should receive a type-correct program rather than discover type errors accidentally.

## Dependency graph

Compact source does not infer scheduling from line order alone. `DependencyAnalyzer` constructs a graph from:

- variable producer/consumer relationships;
- condition references;
- explicit control links;
- execution-effect metadata.

Effect metadata distinguishes concepts such as:

```text
Pure
Read
Write
ExternalMutation
```

and concurrency policies such as:

```text
ParallelSafe
Ordered
Exclusive
```

This is why independent compact LOAD/GET operations can run concurrently while effectful operations remain conservative.

## Execution planning

`ExecutionPlanner` converts the dependency graph into immutable `ExecutionPlanStep` objects containing:

- bound command/frame;
- result binding;
- dependencies;
- retry/timeout/error/condition policy.

Type checking belongs before the planner.

## Runtime execution

`SentenceExecutor` repeatedly selects ready DAG nodes, executes ready work, stores outputs, journals workflow events and handles retry/timeout/conditions/resume.

Dispatch is by stable frame identity to typed routes/handlers.

Execution-result CACHE and `ONCE BY` idempotency are handled in the common `CommandDispatcher`, so the behavior applies across eligible commands without adding cache logic to individual handlers.

## Resource-provider architecture

## Capability and provider architecture

Portable features are described by a `CapabilityDescriptor` and discovered
through `CapabilityRegistry`. A descriptor carries an id, version, supported
platforms and requested permissions. The registry prefers an exact platform
provider and falls back to a provider marked `Any`:

```text
Capability requirement
        -> CapabilityRegistry
        -> exact platform provider
        -> portable fallback provider
        -> unavailable capability diagnostic
```

This keeps language and workflow source platform-neutral. Windows, Linux and
macOS integrations belong in providers, not in the core command grammar.

Compact GET/LOAD uses:

```text
Surface value
   -> resource classification/inference
   -> ResourceDescriptor
   -> IResourceProviderRegistry.Resolve
   -> provider.LowerRead(...)
   -> canonical CommandSyntax
```

Built-in providers currently cover:

- local files;
- portable file-pattern scanning through the `filesystem.scan` capability;
- portable SHA-256 hashing through the `filesystem.hash` capability;
- portable runtime information through the `system.info` capability;
- portable host folder resolution through the injectable `system.path` capability;
- portable clock and cancellation-aware delay through the `system.time` capability;
- portable copy/move operations through the `filesystem.write` capability;
- recoverable trash operations through the `filesystem.trash` capability;
- host-driven file events through the `filesystem.watch` capability;
- portable key-value state through the `storage.keyvalue` capability;
- provider-neutral text blobs through the `storage.blob` capability;
- optional local SQLite queries through the `database.sql` capability;
- explicitly configured direct process execution through the `system.process` capability;
- portable ZIP creation/extraction through the `filesystem.archive` capability;
- HTTP GET/POST/PUT/PATCH/DELETE/download through the `network.http` capability;
- desktop clipboard reads and writes through the `system.clipboard` capability;
- portable directory creation through the `filesystem.directory` capability;

The CLI exposes `--dry-run` and interactive `:dry-run`; these analyze and print
canonical commands without dispatching any effectful handler.
- HTTP JSON;
- environment variables;
- secrets.

Unknown schemes become `ModuleResourceReference`, enabling custom providers registered with:

```csharp
module.ResourceProvider<MyResourceProvider>();
```

## Data language

FILTER/SORT/TAKE/SELECT/MAP/DEFAULT/GROUP/SUM/JOIN/MATCH compile to typed JSON transform commands. They do not create a separate query runtime.

Pipeline chaining is implemented with ordinary output/input variables and therefore participates in the same type checker and dependency graph.

FOR EACH compiles its supported body actions into a descriptor and executes with bounded concurrency using isolated iteration variable scopes.

## Automation architecture

`AutomationCompiler` separates trigger metadata from workflow execution:

```text
EVERY / WATCH / WHEN source
        -> TriggerDefinition
        + WorkflowTemplate(SurfaceCompilationResult)
```

`AutomationScheduler` is host-driven. It owns no thread and receives `TickAsync(now)` or `PublishSignalAsync(...)` calls from the embedding host. It executes the workflow template's normal `ExecutionPlan` with `SentenceExecutor`.

`DurableAutomationScheduleStore` persists timer schedule state only. The host recompiles/re-registers definitions after restart.

## ENSURE architecture

`EnsureCompiler` parses desired-state goals and constructs ordinary surface GET/SAVE syntax programmatically. Optional `REFRESH EVERY` produces an automation definition using the same compiled plan.

`EnsureRunner` adds desired-state runtime concerns such as version retention and failure notification around that existing plan.

## Durable workflow architecture

Workflow durability stays behind `IWorkflowStateStore`. The executor writes the same `WorkflowEvent` protocol regardless of whether the host chooses:

- `InMemoryWorkflowStateStore`;
- simple `JsonFileWorkflowStateStore`;
- checksummed `DurableWorkflowStateStore`;
- a custom store.

Resume checks the plan fingerprint before restoring terminal steps.

## Runtime model

Stable frames and typed commands are the only module/runtime architecture. There
is no compatibility projection or alternate execution path.

## What is intentionally not another runtime

The following features do **not** get their own executor:

- compact syntax;
- data pipelines;
- TASK;
- policies;
- automation workflow bodies;
- ENSURE GET/SAVE plans.

This keeps error handling, retries, workflow journaling, capabilities and result binding concentrated in one execution model.
