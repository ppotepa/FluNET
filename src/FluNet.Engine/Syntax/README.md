# Syntax Folder Structure

This folder contains all interfaces and classes that define the FluNET language syntax and validation.

## Folder Organization

### 📁 Core/
**Fundamental word type interfaces that form the foundation of the FluNET language.**

- **IWord.cs** - Base interface for all words in the language. Provides bidirectional navigation (Next/Previous) for traversing word chains.
- **IVerb.cs** - Verb interface that extends IWord. Verbs are action words and implement validation. Contains both non-generic and generic `IVerb<TWhat, TFrom>` versions.
- **INoun.cs** - Noun interface that extends IWord. Nouns represent objects, places, things, or concepts.

### 📁 Nouns/
**Specialized noun interfaces representing grammatical roles (prepositions, objects, etc.).**

All interfaces in this folder extend `INoun` and `IKeyword`, making them concrete parts of speech.

- **IWhat\<T\>** - Direct object interface. Represents the object being acted upon (e.g., "GET **[data]**").
- **IFrom\<T\>** - Source/origin preposition. Indicates where something comes from (e.g., "FROM **[file.txt]**").
- **ITo\<T\>** - Destination preposition. Indicates where something goes to (e.g., "TO **[output.txt]**").
- **IWith\<T\>** - Accompaniment preposition. Indicates what accompanies an action (e.g., "WITH **[credentials]**").
- **IUsing\<T\>** - Instrument preposition. Indicates what tool/method is used (e.g., "USING **[algorithm]**").

### 📁 Validation/
**Everything related to sentence structure validation.**

- **IValidatable.cs** - Interface for words that can validate what comes next in a sentence.
- **ValidationResult.cs** - Immutable result object containing validation success/failure state and error messages.
- **SentenceValidator.cs** - Service class that validates entire sentences, checking structure and terminator requirements.

### 📁 Attributes/
**Metadata attributes for syntax elements.**

- **RequiredKeywordsAttribute.cs** - Attribute to mark which keywords are required for a particular verb or construct.

### 📁 Verbs/
**Concrete verb implementations.**

- **Get.cs** - Abstract base class for GET verb with generic type parameters `<TWhat, TFrom>`.
- **GetText.cs** - Concrete implementation that gets text from a file.

## Interface Hierarchy

```
IWord (base - provides Next/Previous navigation)
│
├── IVerb : IWord, IKeyword, IValidatable
│   │   Purpose: Action words that can be executed and validated
│   │   Examples: GET, POST, DELETE, SAVE
│   │
│   └── IVerb<TWhat, TFrom> : IWord, IKeyword, IValidatable
│       Purpose: Type-safe verbs with explicit input/output types
│       Example: Get<string[], FileInfo> - gets string array from file
│
└── INoun : IWord
    │   Purpose: Objects, places, things, or concepts
    │
    ├── IWhat<T> : INoun, IKeyword
    │   Purpose: Direct object (thing being acted upon)
    │   Example: GET [data] - where [data] is IWhat<string[]>
    │
    ├── IFrom<T> : INoun, IKeyword
    │   Purpose: Source/origin preposition
    │   Example: FROM [file.txt] - where [file.txt] is IFrom<FileInfo>
    │
    ├── ITo<T> : INoun, IKeyword
    │   Purpose: Destination preposition
    │   Example: TO [output.txt] - where [output.txt] is ITo<FileInfo>
    │
    ├── IWith<T> : INoun, IKeyword
    │   Purpose: Accompaniment preposition
    │   Example: WITH [credentials] - where [credentials] is IWith<AuthToken>
    │
    └── IUsing<T> : INoun, IKeyword
        Purpose: Instrument/method preposition
        Example: USING [AES256] - where [AES256] is IUsing<Algorithm>

IValidatable (behavior interface)
│   Purpose: Enables validation of word sequences
│   Method: ValidateNext(IWord nextWord, Lexicon lexicon)
│   Implemented by: IVerb and preposition classes
```

## Grammatical Structure Visualization

```
Sentence: GET [data] FROM [file.txt] AND SAVE TO [output.txt].

Word Chain:
┌─────────┐    ┌────────┐    ┌──────┐    ┌───────────┐
│   GET   │───▶│ [data] │───▶│ FROM │───▶│[file.txt] │───▶ ...
│  IVerb  │    │ IWhat  │    │IFrom │    │   IFrom   │
└─────────┘    └────────┘    └──────┘    └───────────┘
    │              │             │              │
Validates───────────▶       ────────▶      ─────────▶
  Next Word        Valid     Valid         Valid

Navigation:
◀────Previous────────Next────▶
```

## Design Principles

1. **Navigation**: All words (IWord) support bidirectional navigation through Next/Previous properties, similar to a doubly-linked list.

2. **Validation**: Verbs implement IValidatable to enforce correct sentence structure. They validate what type of word can follow them.

3. **Type Safety**: Generic type parameters ensure compile-time type safety for data flow (e.g., `IVerb<TWhat, TFrom>` ensures the verb processes data of the correct types).

4. **Composability**: Interfaces can be composed to create complex sentence structures while maintaining clear grammatical rules.

5. **Keyword Integration**: All verbs and specialized nouns implement IKeyword, allowing them to be recognized as specific language constructs.

## Usage Example

```csharp
// A sentence like "GET [data] FROM [file.txt]" would be structured as:
// - GET: IVerb<string[], FileInfo> (verb)
// - [data]: IWhat<string[]> (direct object)
// - FROM: IFrom<FileInfo> (source preposition)
// - [file.txt]: The value (FileInfo)
```

## Adding New Syntax Elements

### To add a new preposition/noun type:
1. Create a new interface in `Nouns/` that extends `INoun, IKeyword`
2. Add a generic type parameter for the data it carries
3. Add a property to access the data

### To add a new verb:
1. Create an abstract base class in `Verbs/` that extends `IVerb<TWhat, TFrom>`
2. Implement the validation logic in `ValidateNext()`
3. Create concrete implementations for specific use cases

### To add validation logic:
1. Implement `IValidatable` interface
2. Override `ValidateNext()` to check if the next word is valid
3. Return `ValidationResult.Success()` or `ValidationResult.Failure(reason)`
