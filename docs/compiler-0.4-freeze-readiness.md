# FluNET 0.4 compiler freeze readiness

This document supersedes the earlier preview status for implementation tracking.
The source tree contains the 0.4 compiler building blocks, but
`StandardLanguageIdentity.Version` intentionally remains `0.3` until a real
Release build and test run verifies the complete tree.

## Batch status

### Batch 10 — value codecs and conversion graph

Implemented.

- language-owned parser/formatter/codec contracts;
- implicit and explicit conversion descriptors;
- positive-cost shortest-path resolution;
- ambiguity and cycle protection;
- built-in Text/Boolean/Number/File/Directory/Uri/Json/Encoding codecs;
- built-in Text boundary conversions;
- module `Codec<TValue,TCodec>()` and
  `Conversion<TSource,TTarget,TConversion>()` registration;
- typed compilation resolves the runtime module registry.

The value registry treats `Number -> Text` and equivalent boundaries as real
conversion edges, never registry identity.

### Batch 11 — expression binding

Infrastructure implemented; compatibility adapters remain inside several
built-in binders.

- `ExpressionBinder` is the central role/value binder;
- `CommandBindingContext` exposes `Require`, `Optional`, `Repeated`, and text
  binding;
- registry-backed expression codecs emit FLN140–FLN143;
- existing expression node types are reused rather than duplicated.

Several 0.3 built-in binder classes still construct their historical expression
helpers after the compiler has validated the same slots. Removing those helper
calls is cleanup, not a second execution pipeline, but it is not represented as
complete in this readiness document.

### Batch 12 — typed command IR

Implemented for the module execution path.

- `TypedProgram` / `CompiledCommand` are produced before planning;
- `CommandCompilationStep` rejects malformed typed literals before execution;
- `CompiledCommandRoute<TCommand,TResult>` caches the typed command by immutable
  `BoundCommand` identity;
- module route registration resolves to the compiled-route registration while
  the old direct-DI `AddTypedCommand` remains the 0.3 compatibility API;
- handlers receive the typed command value; route cache contracts assert that a
  compiled route binds once.

### Batch 13 — typed variable store

Implemented.

- `RuntimeValue(TypeSymbol, object)` is the store value;
- Host, Workflow, Block and Iteration scopes exist;
- lookup order is Iteration -> Block -> Workflow -> Host;
- declaration and assignment validate the language type;
- accidental type changes are rejected;
- `VariableResolver` is an adapter over the typed store rather than an
  independent object dictionary.

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
A single `=` is rejected instead of being treated as equality.

The command tokenizer also tracks parenthesis depth before interpreting command
connectors, so `IF ([a] AND [b])` remains one command while top-level
`command AND command` remains a parallel link.

### Batch 15 — typed program validation

Implemented for producer flow and parallel-write invariants.

Stable codes:

```text
FLN150 unresolved/unavailable variable
FLN151 incompatible types
FLN152 ambiguous conversion
FLN153 parallel write conflict
FLN154 invalid condition expression/type
```

Producer/consumer types and same-stage availability are checked before
execution. The compatibility planner still retains some older validation and
host-variable discovery, so it has not yet been reduced to a purely structural
DAG builder.

### Batch 16 — typed conditions

Implemented without adding a second executor.

- `ExpressionSyntaxParser` creates the condition AST;
- `ConditionExpressionCompiler` compiles Boolean, comparison, arithmetic,
  property/index, list/object and parenthesized nodes;
- `CompiledCondition` contains `IExpression<bool>` and variable dependencies;
- `ConditionExpressionCache` reuses side-effect-free compiled trees;
- condition compilation is part of typed command compilation/validation;
- the existing executor delegates complex condition resolution through the
  context resolver to the cached typed expression;
- ELSE continues to use the existing inversion flag over the typed Boolean
  result.

### Batch 17 — freeze

Implementation candidate only; version bump intentionally deferred.

New contract tests cover:

- value registry and custom module codecs;
- expression precedence and structural nodes;
- typed variable-store rules;
- compile-time invalid literals;
- parallel write conflicts;
- compiled-route bind-once behavior;
- typed conditions and produced-variable flow;
- side-effect-free `AnalyzeTyped`.

`FluNETContext.AnalyzeTyped(...)` is the additive 0.4 analysis API. It first uses
the source-compatible `Engine.Analyze` parse/bind/semantic analysis and then
compiles that same `BoundProgram` into `TypedProgram`. This avoids a breaking
change to the existing `Engine.Analyze` return type while making typed command
compilation part of 0.4 validity.

## Remaining compatibility cleanup

Before changing the public language version to `0.4`, complete these cleanup
items and verify them in a real build:

1. migrate the remaining built-in binder implementations from historical
   expression helper construction to `CommandBindingContext`;
2. remove the transitional non-Unit-to-Text rule from
   `TypeSymbol.IsAssignableFrom` after every compatibility caller uses the
   conversion registry;
3. move the last host-variable/type checks out of `ExecutionPlanner` so planning
   is structural only;
4. expose Compile/TypeCheck as first-class `CompilationPhase` values once the
   compatibility diagnostic contract is deliberately versioned;
5. remove superseded preview test source files currently excluded by the test
   project cleanup target.

No release/version claim should be made until `dotnet build` and `dotnet test`
have run against this exact tree.
