# FluNET Classic architecture

The target architecture keeps the sentence abstraction at every public extension boundary.

```text
source
  -> lexer/tokenization
  -> sentence parser
  -> AST
  -> binder
       -> LanguageRegistry
       -> ValueResolverRegistry
       -> CLR type system
  -> bound script
  -> ExecutionPipeline
  -> runtime
```

## LanguageRegistry

`LanguageRegistry` is the single catalog for words, verbs, synonyms, qualifiers and sentence patterns. Reflection is confined to module registration. Token lookup is dictionary-based.

## SentencePattern

The existing `IWhat<T>`, `IFrom<T>`, `ITo<T>`, `IUsing<T>`, `IWith<T>` and `IThen<T>` contracts are promoted to semantic language roles. The registry derives a `SentencePattern` from those contracts. Parsing, validation, tooling and documentation can therefore share one grammar model.

## AST and binding

AST nodes contain syntax only. The binder maps syntax to registered verbs and CLR types, resolves values and produces diagnostics before execution. This separation makes future `WHERE`, property access, interpolation and control sentences possible without coupling them to individual verbs.

## Extensions

Language packages implement `IFluNetModule`. Typical modules are expected to be separate NuGet packages such as `FluNET.Classic.Http`, `FluNET.Classic.Json`, `FluNET.Classic.Sql` and `FluNET.Classic.Azure`.

Runtime extensions remain independent and continue to use the execution pipeline for tracing, retry, authorization, transactions, caching, sandboxing and telemetry.
