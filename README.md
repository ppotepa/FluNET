# FluNET

**Author:** Paweł Potępa

FluNET is an experimental C#/.NET engine for writing small programs as English‑like commands instead of method calls. It focuses on language design, internal DSLs and metaprogramming, not on being a full general‑purpose language.

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

1. **Tokenization** – raw input is split into typed tokens (words, variables `[x]`, references like `file.txt`).
2. **TokenTree** – tokens are arranged into a small AST‑like structure representing the sentence.
3. **Verb resolution** – the engine finds a verb matching the root token and expected arguments.
4. **Execution** – a strongly‑typed verb object is created and invoked.

## Core concepts

- **Verbs** implement a generic interface:

  ```csharp
  public interface IVerb<TWhat, TFrom> : IVerb
  {
      Func<TFrom, TWhat> Act { get; }
      TFrom? Resolve(string value);
      TWhat Invoke();
  }
  ```

- **Tokens & TokenTree** capture the structure of a command in a simple AST.
- **DiscoveryService** uses reflection to find available verbs and their "grammar".
- **SentenceExecutor** builds and runs verb instances from the TokenTree.

## Status

- Target runtime: **.NET / C#**
- Focus: experiments with natural‑language‑inspired syntax, type safety and reflection‑based discovery.
- Test coverage is high; remaining gaps are mostly around test infrastructure, not core engine logic.
