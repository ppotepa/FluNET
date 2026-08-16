# FluNET.Classic

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0+-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)

**Author:** Paweł Potępa

FluNET.Classic is a sentence-oriented, typed and extensible scripting language hosted by .NET. It evolves the original FluNET experiment from the compatibility baseline `55455b46ed84fb447a8d47ba89772538944eb7b3` while preserving its English-like sentence syntax.

```text
GET [text] FROM file.txt
SAVE [content] TO output.txt
DOWNLOAD [data] FROM https://api.example.com
```

The long-term goal is not to clone PowerShell. PowerShell is command/object-pipeline oriented; FluNET.Classic is **sentence/typed-sentence composition oriented**. CLR types, dependency injection and NuGet remain the underlying ecosystem.

## Language invariants

1. Existing Classic sentences are compatibility contracts and should not silently change meaning.
2. Public extension APIs should mirror sentence concepts: verbs, semantic clauses, qualifiers and modules.
3. `IWhat<T>`, `IFrom<T>`, `ITo<T>`, `IUsing<T>`, `IWith<T>` and `IThen<T>` are language roles, not implementation accidents.
4. Reflection is allowed during module discovery, but parsing/token lookup must not repeatedly scan assemblies.
5. FluNET types project CLR types instead of creating an unrelated runtime type universe.
6. `THEN` evolves into typed composition while preserving the existing explicit variable form.

## Current architecture

```text
source
  -> tokenization
  -> sentence creation
  -> validation
  -> execution pipeline
```

Classic now introduces the migration architecture alongside the existing runtime:

```text
source
  -> lexer/tokenization
  -> sentence parser
  -> AST
  -> binder
       -> LanguageRegistry
       -> ValueResolverRegistry
       -> CLR-backed type system
  -> bound script
  -> ExecutionPipeline
```

`LanguageRegistry` is the single language catalog. It discovers words once, indexes verbs and synonyms, exposes extensible qualifiers and derives a `SentencePattern` directly from the existing typed clause interfaces.

## Evolving the language

Language packages should contribute vocabulary instead of modifying the engine:

```csharp
public sealed class ParquetModule : IFluNetModule
{
    public void Configure(LanguageRegistry language)
    {
        language.RegisterQualifier("PARQUET");
    }
}
```

Natural future packages include `FluNET.Classic.Http`, `FluNET.Classic.Json`, `FluNET.Classic.Sql`, `FluNET.Classic.Azure` and domain-specific modules.

Planned surface additions are deliberately orthogonal: `AS`, property access, interpolation, `WHERE`, typed implicit `THEN`, then sentence-shaped `IF/ELSE` and `FOR EACH`. The established `GET/SAVE/... FROM/TO/USING` syntax remains valid throughout that evolution.

See [docs/LANGUAGE.md](docs/LANGUAGE.md) for the language contract and [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the migration architecture.

## License

MIT.
