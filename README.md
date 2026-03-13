# FluNET

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0+-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)

**Author:** Paweł Potępa

FluNET is an experimental C#/.NET engine for expressing small programs as English‑like commands instead of method calls. It focuses on language design, internal DSLs, and metaprogramming rather than being a full general‑purpose language.

## Example

```text
GET [text] FROM file.txt
SAVE [content] TO output.txt
DOWNLOAD [data] FROM https://api.example.com
```

Roughly equivalent to:

```csharp
var text = File.ReadAllText("file.txt");
File.WriteAllText("output.txt", content);
var data = await httpClient.GetAsync("https://api.example.com");
```

## Architecture

Pipeline:

1. **Tokenization** – raw input is split into `RawToken` and `Token` objects (`TokenFactory`).
2. **TokenTree** – tokens are arranged into a small AST‑like structure.
3. **Sentence building** – `SentenceFactory` + `DiscoveryService` resolve verbs and arguments.
4. **Validation** – `SentenceValidator` checks that the sentence is syntactically valid.
5. **Execution** – `SentenceExecutor` runs a strongly‑typed verb instance.

All of this is orchestrated by the `Engine` using an `ExecutionPipeline` composed of small `IExecutionStep` processors (parse, validate, execute, error handling, etc.).

## Keywords

Keywords are first‑class types implementing `IKeyword`/`IWord` (for example `Get`, `Save`, `Post`, `Delete`, `Load`, `Send`, `Transform`). They define the textual surface of verbs (e.g. `"GET"`) and participate in validation. `DiscoveryService` scans assemblies to discover available keywords and verbs.

## Evolving the language

To grow the language, you extend the vocabulary and verbs rather than touching the core engine:

1. **Add keywords** – create `IKeyword`/`IWord` types for new verbs or prepositions (e.g. `Compress`, `Using`).
2. **Implement verbs** – add `IVerb<,>` implementations that model real actions in your domain, plus `IWhat<T>`, `IFrom<T>`, `ITo<T>` etc. as needed.
3. **Load assemblies** – ensure assemblies with your keywords/verbs are loaded so `DiscoveryService` can discover them.
4. **Document usage** – add example sentences and tests so the new constructs stay stable as the engine evolves.

## License

This project is licensed under the [MIT License](LICENSE).
