# FluNET 0.4 compiler freeze readiness

This document is the implementation gate for the 0.4 compiler milestone.
`StandardLanguageIdentity.Version` intentionally remains `0.3` until a real
Release build and test run verifies the exact candidate tree.

## Canonical pipeline

The standard host now uses one pipeline:

```text
Parse
  -> Bind
  -> Validate
  -> Compile
  -> TypeCheck
  -> Plan
  -> Execute
```

`CompilationPhase` exposes all six side-effect-free compiler phases before
execution. `Engine.Analyze` remains the source-compatible parse/bind/validate/
plan view; `FluNETContext.AnalyzeTyped` adds typed command compilation and type
checking without executing handlers.

## Batch status

### Batch 10 — value codecs and conversion graph

Implemented.

- language-owned parser/formatter/codec contracts;
- implicit and explicit conversion descriptors;
- positive-cost shortest-path resolution;
- ambiguity and cycle protection;
- built-in Text/Boolean/Number/File/Directory/Uri/Json/Encoding codecs;
- real conversion edges instead of `TypeSymbol` conversion shortcuts;
- standard `List<Text> -> Text` conversion for GET/LOAD line producers;
- module `Codec<TValue,TCodec>()` and
  `Conversion<TSource,TTarget,TConversion>()` registration;
- `ValueCodecRegistryFactory.CreateDefault` provides the same core conversion
  contract to compatibility constructors and side-effect-free tests.

`TypeSymbol.IsAssignableFrom` is now purely structural. Runtime expression
conversion goes through `IValueCodecRegistry`; there is no arbitrary CLR
`ToString()` fallback in the canonical expression codec.

### Batch 11 — expression binding

Implemented for all built-in command binders.

- `ExpressionBinder` is the central role/value binder;
- `CommandBindingContext` exposes `Require`, `Optional`, `Repeated`, and text
  binding;
- registry-backed expression codecs emit FLN140–FLN143;
- GET/LOAD/SAVE/DELETE/DOWNLOAD/POST/SAY/SEND/TRANSFORM/SET/PARSE/FORMAT bind
  through the shared expression context;
- historical `ScalarExpression`, `TextExpression`, `JsonExpression`, and value
  converter classes remain compatibility APIs, not the standard binder path;
- `FrameCommandBinder` retains protected compatibility constructors for existing
  extension binders.

### Batch 12 — typed command IR

Implemented for the module execution path.

- `TypedProgram` / `CompiledCommand` are produced before planning;
- malformed typed literals fail during Compile before any handler executes;
- `CompiledCommandRoute<TCommand,TResult>` caches the typed command by immutable
  `BoundCommand` identity;
- module route registration uses compiled routes while direct-DI
  `AddTypedCommand` remains the 0.3 compatibility API;
- canonical module execution reuses the compiled command rather than rebinding
  arguments at each step.

### Batch 13 — typed variable store

Implemented.

- `RuntimeValue(TypeSymbol, object)` is the store value;
- Host, Workflow, Block and Iteration scopes exist;
- lookup order is Iteration -> Block -> Workflow -> Host;
- declaration and assignment validate structural language types;
- accidental type changes are rejected;
- `VariableResolver` is an adapter over `VariableStore`, not an independent
  `Dictionary<string, object>` source of truth;
- host variables registered through `Engine.RegisterVariable` participate in
  typed analysis with the active `LanguageSnapshot`.

### Batch 14 — expression syntax AST

Implemented as a standalone expression grammar.

Nodes:

- literal;
- variable;
- unary;
- binary;
- parenthesized;
- property;
- index;
- list;
- object.

Precedence:

```text
OR
AND
== !=
< <= > >=
+ -
* /
NOT ! unary-
postfix
primary
```

`LIST(...)` and `OBJECT(...)` avoid collision with FluNET variable syntax.
A single `=` is tokenized safely and rejected by the parser rather than looping.
The command parser tracks parenthesis depth before interpreting connectors, so
`IF ([a] AND [b])` stays inside one command while top-level
`command AND command` remains a parallel link.

### Batch 15 — typed program validation

Implemented before planning.

Stable codes:

```text
FLN150 unresolved/unavailable variable
FLN151 incompatible types
FLN152 ambiguous conversion
FLN153 parallel write conflict
FLN154 invalid condition expression/type
```

The type validator checks:

- producer/consumer flow;
- same-stage availability;
- host-variable existence and language type;
- implicit conversion paths;
- ambiguous conversions;
- condition-variable dependencies;
- parallel writes to the same target.

`ExecutionPlanner` is now structural: it builds sequence/data dependencies,
policies, result bindings and the DAG, but no longer decides type compatibility
or parallel-write validity.

### Batch 16 — typed conditions

Implemented without a second executor.

- `ExpressionSyntaxParser` creates the condition AST;
- `ConditionExpressionCompiler` handles Boolean logic, equality/order,
  arithmetic, unary operators, property/index access, lists/objects and
  parenthesized expressions;
- `CompiledCondition` contains `IExpression<bool>` and referenced variables;
- `ConditionExpressionCache` reuses side-effect-free compiled trees;
- condition syntax is compiled before planning;
- planner dependencies are derived from `CompiledCondition.VariableReferences`;
- the existing executor evaluates the cached `IExpression<bool>` against the
  current resolver; it no longer implements its own string truthiness parser;
- ELSE preserves the existing inversion policy over the typed Boolean result.

### Batch 17 — freeze candidate

Implementation complete; release verification is still pending.

Contract tests now cover, among other things:

- value registry and custom module codecs;
- Number/Text and List<Text>/Text conversion flow;
- expression precedence and structural AST nodes;
- typed variable-store scope/type rules;
- host variables in typed analysis;
- compile-time invalid literals;
- unresolved command and condition variables;
- parallel write conflicts;
- compiled-route bind-once behavior;
- parenthesized typed conditions;
- side-effect-free `AnalyzeTyped` compile + type-check validity.

Superseded preview test sources and their MSBuild exclusion file have been
removed; the active tests are the only source of the 0.4 contract.

## Release gate

No version or release claim should be made until the following commands have
run successfully against this exact tree with .NET 8 available:

```bash
dotnet restore FluNET.sln
dotnet build FluNET.sln --configuration Release --no-restore
dotnet test FluNET.sln --configuration Release --no-build
```

After a green run:

1. fix any compiler/test regressions as isolated stabilization commits;
2. set `StandardLanguageIdentity.Version` to `0.4`;
3. replace the preview compiler contract snapshot with the final 0.4 snapshot;
4. update release-facing README/migration wording;
5. tag the verified commit only if the publishing workflow for the repository is
   available and intentionally requested.

Until that gate is satisfied, the code is a **0.4 freeze candidate** and the
published language identity remains `0.3`.
