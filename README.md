# FluNET

**Author:** Paweł Potępa

This is a thinking, training, and experimental project exploring natural language programming in C#/.NET.

## What Is This?

FluNET allows you to write executable code using English-like sentences instead of traditional method calls. Think of it as turning natural language into structured, type-safe programming.

Write this:
```
GET [text] FROM file.txt
SAVE [content] TO output.txt
DOWNLOAD [data] FROM https://api.example.com
```

Instead of this:
```csharp
var text = File.ReadAllText("file.txt");
File.WriteAllText("output.txt", content);
var data = httpClient.GetAsync("https://api.example.com");
```

## How It Works

The system mirrors how we construct sentences in natural language. Just as English sentences have subjects, verbs, and objects, FluNET sentences have a similar structure:

```
[VERB] + [WHAT] + [PREPOSITION] + [SOURCE/DESTINATION]
```

### Architecture Flow

```
User Input  →  Tokenization  →  TokenTree  →  Sentence  →  Execution
                                                              
"GET [x] FROM file.txt"                                      result
     ↓                                                          ↑
[Word:GET] [Var:x] [Word:FROM] [Ref:file.txt]                 │
     ↓                                                          │
   Root: GET                                                    │
    ├── WHAT: x                                                 │
    └── FROM: file.txt                                          │
     ↓                                                          │
GetText Instance                                                │
    What = "x"                                                  │
    From = "file.txt"                                           │
     ↓                                                          │
Invoke() ────────────────────────────────────────────────────>│
```

The process is analogous to how your brain parses language: recognize words, understand their relationships, determine meaning, then act.

## Key Components

### 1. Tokens and TokenTree

Like sentence diagramming in grammar class, we break input into tokens and organize them hierarchically. The TokenTree represents the grammatical structure of your command.

### 2. Discovery System

The DiscoveryService scans your assemblies to find all available "vocabulary" - verbs, nouns, and prepositions (keywords). This is how the system learns what commands exist.

### 3. Verbs

Verbs are the action words. Each verb implements this interface:

```csharp
public interface IVerb<TWhat, TFrom> : IVerb
{
    Func<TFrom, TWhat> Act { get; }  // The actual behavior
    TFrom? Resolve(string value);     // Parse input text to typed value
    TWhat Invoke();                   // Execute the action
}
```

Supporting interfaces like `IWhat<T>`, `IFrom<T>`, `ITo<T>`, and `IUsing<T>` work like grammatical cases, defining the role each part plays in the sentence.

### 4. Sentence Executor

This component builds verb instances from the TokenTree and executes them. It's the bridge between syntax (structure) and semantics (meaning).

## Implementing New Verbs

Creating a new verb is like defining a new action in your domain language:

```csharp
public class Compress<TWhat, TTo> : IVerb<TWhat, TTo>, 
    IWhat<TWhat>, ITo<TTo>
{
    public TWhat What { get; protected set; }
    public TTo To { get; protected set; }
    
    public Func<TTo, TWhat> Act => (destination) => 
    {
        // Your compression logic
        return CompressData(What, destination);
    };
    
    public TWhat Invoke() => Act(To);
    
    public bool Validate(IWord word) => 
        word is IWhat<TWhat> || word is ITo<TTo>;
    
    public TTo? Resolve(string value) => 
        (TTo)Convert.ChangeType(value, typeof(TTo));
}
```

Then create concrete implementations:

```csharp
public class CompressFile : Compress<byte[], string>
{
    public override Func<string, byte[]> Act => (filePath) =>
    {
        var data = File.ReadAllBytes(filePath);
        return GZipCompress(data);
    };
}
```

Now you can write: `COMPRESS [data] TO archive.gz`

### Growing Your Own Verbs

To extend the language in your own domain:
- Define a verb class implementing `IVerb<,>` plus the noun interfaces you need (`IWhat<T>`, `IFrom<T>`, `ITo<T>`, etc.).
- Implement `Validate` to declare which words/positions are allowed.
- Implement `Resolve` to turn raw text into typed values.
- Register the verb so `DiscoveryService` can find it.
- Document an example sentence in your README so others can reuse it.


## Connection to Natural Language

The design deliberately parallels linguistic concepts:

- **Verbs** = Action words (GET, SAVE, DOWNLOAD)
- **Nouns** = Things being acted upon (text, file, json)
- **Prepositions** = Relationships (FROM, TO, USING)
- **Variables** = Pronouns, represented as [variableName]
- **References** = Proper nouns, like file names {file.txt}

Just as language has syntax rules (you can't say "FROM GET file.txt text"), FluNET validates sentence structure. The `Validate()` method ensures words appear in sensible contexts.

## Current State

The project achieves 88.8% test coverage (293/330 tests passing). The remaining tests primarily involve infrastructure setup (test web server) rather than code defects.

## Why This Matters

This experiment explores whether natural language structures can make programming more intuitive without sacrificing type safety or performance. It's a study in domain-specific languages, demonstrating how reflection, generics, and careful interface design can create a bridge between human expression and machine execution.

## Recommended GitHub Topics

Apply the following Topics (via Settings → Topics) to highlight FluNET's unique combination of linguistic structure, DSL design, and .NET tooling. Each tag signals a searchable angle that aligns with how the project currently works:

- `natural-language-programming` — signals the primary goal of expressing code as English-like sentences.
- `domain-specific-language` — shows that FluNET exposes a custom grammar and vocabulary.
- `dsl` — reinforces that FluNET is a domain-specific language and not a general-purpose compiler.
- `csharp` — ties the project to its native language so C# developers can discover it.
- `dotnet` — connects to the broader .NET ecosystem.
- `command-interpreter` — highlights the syntax parsing and execution pipeline.
- `tokenization` — points at the tokenizer, TokenTree, and token factories that underpin sentence parsing.
- `reflection` — reflects the use of runtime discovery, verbs, and word metadata.
- `experimental` — flags that this is a research/training project exploring a novel interface.

Add more topics as new capabilities emerge (e.g., `file-processing`, `compression`, or `cli-tools`) so GitHub search keeps reflecting the repo's growth.
