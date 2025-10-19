# FluNET Syntax Interface Categorization

This document provides a detailed categorization of all interfaces that implement `IWord` and their roles in the FluNET language syntax.

## Table of Contents
1. [Overview](#overview)
2. [Core Interfaces](#core-interfaces)
3. [Noun Interfaces](#noun-interfaces)
4. [Validation System](#validation-system)
5. [Implementation Guidelines](#implementation-guidelines)

---

## Overview

The FluNET syntax system is built on a hierarchical interface model where all language elements implement `IWord`. This provides:
- **Uniform navigation** through Next/Previous properties (doubly-linked list pattern)
- **Type safety** through generic type parameters
- **Grammatical validation** through the IValidatable interface
- **Composability** to build complex sentence structures

---

## Core Interfaces

### 📁 Location: `Syntax/Core/`

These are the foundational interfaces from which all other syntax elements derive.

#### IWord
```csharp
public interface IWord
{
    IWord? Next { get; set; }
    IWord? Previous { get; set; }
}
```

**Category:** Base Interface  
**Purpose:** Foundation for all word types in the language  
**Provides:** Bidirectional navigation through word chains  
**Used by:** All verbs, nouns, and language constructs  
**Analogy:** Like a node in a doubly-linked list

---

#### IVerb (non-generic)
```csharp
public interface IVerb : IWord, IKeyword, IValidatable
{
    Func<object> Act { get; }
}
```

**Category:** Core Word Type  
**Purpose:** Represents action verbs without specific type constraints  
**Extends:** IWord, IKeyword, IValidatable  
**Key Features:**
- Can execute actions via `Act` property
- Can validate subsequent words via `IValidatable`
- Is a recognized keyword via `IKeyword`

**Use Cases:**
- Simple actions without parameters
- Dynamic typing scenarios
- Prototype/testing verbs

---

#### IVerb<TWhat, TFrom> (generic)
```csharp
public interface IVerb<TWhat, TFrom> : IWord, IKeyword, IValidatable
{
    Func<TFrom, TWhat> Act { get; }
}
```

**Category:** Core Word Type (Type-Safe)  
**Purpose:** Represents action verbs with explicit input/output types  
**Type Parameters:**
- `TWhat`: The type of data being produced/acted upon (output)
- `TFrom`: The type of data source (input)

**Extends:** IWord, IKeyword, IValidatable  
**Key Features:**
- Type-safe data flow
- Compile-time type checking
- Clear input/output contracts

**Example:**
```csharp
// Get<string[], FileInfo> means:
// - Input: FileInfo (file source)
// - Output: string[] (lines of text)
public class GetText : Get<string[], FileInfo> { }
```

---

#### INoun
```csharp
public interface INoun : IWord
{
}
```

**Category:** Core Word Type  
**Purpose:** Represents objects, places, things, or concepts  
**Extends:** IWord  
**Key Features:**
- Marker interface for noun classification
- Foundation for specialized noun types (prepositions, objects)

**Use Cases:**
- Direct objects
- Prepositions
- Complements

---

## Noun Interfaces

### 📁 Location: `Syntax/Nouns/`

These interfaces represent specialized grammatical roles. All extend `INoun` and `IKeyword`.

### Categorization by Grammatical Function

| Interface | Type Param | Grammatical Role | Example |
|-----------|------------|------------------|---------|
| `IWhat<T>` | T | Direct Object | GET **[data]** FROM file |
| `IFrom<T>` | T | Source/Origin | GET data FROM **[file.txt]** |
| `ITo<T>` | T | Destination | SAVE data TO **[output.txt]** |
| `IWith<T>` | T | Accompaniment | CONNECT TO server WITH **[credentials]** |
| `IUsing<T>` | T | Instrument/Method | ENCRYPT data USING **[AES256]** |

---

#### IWhat<T>
```csharp
public interface IWhat<out TWhat> : INoun, IKeyword
{
    TWhat What { get; }
}
```

**Category:** Direct Object Noun  
**Grammatical Role:** Direct Object (accusative case)  
**Purpose:** Represents the primary object being acted upon by a verb  
**Type Parameter:** `TWhat` - Type of the object

**Linguistic Pattern:**
```
VERB + WHAT
GET [data]
DELETE [record]
FETCH [resource]
```

**Implementation Example:**
```csharp
public class DataObject : IWhat<byte[]>
{
    public byte[] What { get; set; }
}
```

**Real-world Usage:**
- File paths: `IWhat<string>`
- Data buffers: `IWhat<byte[]>`
- Records: `IWhat<Record>`
- Collections: `IWhat<List<T>>`

---

#### IFrom<T>
```csharp
public interface IFrom<out TWhat> : INoun, IKeyword
{
    TWhat From { get; }
}
```

**Category:** Prepositional Noun  
**Grammatical Role:** Source/Origin (ablative case)  
**Purpose:** Indicates where something originates or is retrieved from  
**Type Parameter:** `TWhat` - Type of the source

**Linguistic Pattern:**
```
VERB + FROM + SOURCE
GET data FROM [file.txt]
READ content FROM [database]
DOWNLOAD file FROM [url]
```

**Common Source Types:**
- Files: `IFrom<FileInfo>`
- URLs: `IFrom<Uri>`
- Databases: `IFrom<DbConnection>`
- Streams: `IFrom<Stream>`

---

#### ITo<T>
```csharp
public interface ITo<out TTo> : INoun, IKeyword
{
    TTo To { get; }
}
```

**Category:** Prepositional Noun  
**Grammatical Role:** Destination (dative/allative case)  
**Purpose:** Indicates where something goes to or is directed  
**Type Parameter:** `TTo` - Type of the destination

**Linguistic Pattern:**
```
VERB + TO + DESTINATION
SAVE data TO [output.txt]
SEND message TO [recipient]
WRITE content TO [stream]
```

**Common Destination Types:**
- Files: `ITo<FileInfo>`
- Recipients: `ITo<EmailAddress>`
- Endpoints: `ITo<Uri>`
- Outputs: `ITo<Stream>`

---

#### IWith<T>
```csharp
public interface IWith<out TWith> : INoun, IKeyword
{
    TWith With { get; }
}
```

**Category:** Prepositional Noun  
**Grammatical Role:** Accompaniment (comitative case)  
**Purpose:** Indicates what accompanies or is used alongside an action  
**Type Parameter:** `TWith` - Type of the accompanying element

**Linguistic Pattern:**
```
VERB + WITH + ACCOMPANIMENT
CONNECT TO server WITH [credentials]
SIGN document WITH [certificate]
AUTHENTICATE WITH [token]
```

**Common Accompaniment Types:**
- Credentials: `IWith<AuthToken>`
- Options: `IWith<Settings>`
- Context: `IWith<HttpContext>`
- Certificates: `IWith<X509Certificate>`

---

#### IUsing<T>
```csharp
public interface IUsing<out TUsing> : INoun, IKeyword
{
    TUsing Using { get; }
}
```

**Category:** Prepositional Noun  
**Grammatical Role:** Instrument/Method (instrumental case)  
**Purpose:** Indicates the tool, method, or means by which an action is performed  
**Type Parameter:** `TUsing` - Type of the instrument

**Linguistic Pattern:**
```
VERB + USING + INSTRUMENT
ENCRYPT data USING [AES256]
HASH password USING [SHA512]
COMPRESS file USING [GZIP]
```

**Common Instrument Types:**
- Algorithms: `IUsing<Algorithm>`
- Protocols: `IUsing<Protocol>`
- Encodings: `IUsing<Encoding>`
- Strategies: `IUsing<IStrategy>`

---

## Validation System

### 📁 Location: `Syntax/Validation/`

### IValidatable
```csharp
public interface IValidatable
{
    ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon);
}
```

**Category:** Behavior Interface  
**Purpose:** Enables words to validate grammatical correctness of subsequent words  
**Implemented By:** Verbs and preposition implementations

**Validation Flow:**
```
GET ──validate──▶ [data] ──validate──▶ FROM ──validate──▶ [file.txt]
 │                   │                    │                     │
 └─ Checks if    └─ Checks if        └─ Checks if         └─ Final
    next is         next is               next is              word
    valid           valid                 valid
```

**Example Implementation:**
```csharp
public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
{
    if (nextWord is IFrom<TFrom>)
        return ValidationResult.Success();
    
    if (nextWord is IWhat<TWhat>)
        return ValidationResult.Success();
    
    return ValidationResult.Failure("Expected FROM or direct object");
}
```

---

### ValidationResult
```csharp
public class ValidationResult
{
    public bool IsValid { get; }
    public string? FailureReason { get; }
    
    public static ValidationResult Success();
    public static ValidationResult Failure(string reason);
}
```

**Category:** Result Object  
**Purpose:** Immutable result of validation operations  
**Pattern:** Result pattern (success/failure with reason)

**Usage:**
```csharp
var result = validator.ValidateNext(nextWord, lexicon);
if (!result.IsValid)
{
    Console.WriteLine($"Validation failed: {result.FailureReason}");
}
```

---

### SentenceValidator
```csharp
public class SentenceValidator
{
    public ValidationResult ValidateSentence(TokenTree tokenTree);
}
```

**Category:** Service Class  
**Purpose:** Validates complete sentence structures  
**Responsibilities:**
1. Check for non-empty sentences
2. Verify valid terminators (., ?, !)
3. Validate word recognition
4. Validate grammatical structure

**Validation Steps:**
1. **Structural Check:** Ensure sentence has content
2. **Terminator Check:** Verify sentence ends properly
3. **Word Recognition:** All words are known
4. **Grammar Check:** Words follow grammatical rules via IValidatable

---

## Implementation Guidelines

### Creating a New Verb

```csharp
// 1. Create abstract base
public abstract class MyVerb<TWhat, TFrom> : IVerb<TWhat, TFrom>, IWhat<TWhat>, IFrom<TFrom>
{
    protected MyVerb(TWhat what, TFrom from)
    {
        What = what;
        From = from;
    }
    
    public TWhat What { get; protected set; }
    public TFrom From { get; protected set; }
    public string Text => "MYVERB";
    public abstract Func<TFrom, TWhat> Act { get; }
    
    public IWord? Next { get; set; }
    public IWord? Previous { get; set; }
    
    public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
    {
        // Implement validation logic
        return ValidationResult.Success();
    }
}

// 2. Create concrete implementation
public class MyVerbConcrete : MyVerb<string, FileInfo>
{
    public MyVerbConcrete(string what, FileInfo from) : base(what, from) { }
    
    public override Func<FileInfo, string> Act => 
        (file) => File.ReadAllText(file.FullName);
}
```

### Creating a New Preposition

```csharp
// Create interface in Syntax/Nouns/
public interface IAround<out TAround> : INoun, IKeyword
{
    /// <summary>
    /// The element or location around which an action occurs.
    /// </summary>
    TAround Around { get; }
}

// Usage: SEARCH AROUND [location] FOR [item]
```

### Best Practices

1. **Type Safety:** Always use generic type parameters for data flow
2. **Validation:** Implement comprehensive validation in `ValidateNext()`
3. **Documentation:** Add XML comments to all public interfaces
4. **Testing:** Create unit tests for validation logic
5. **Naming:** Use clear, grammatically correct names
6. **Immutability:** Prefer immutable data where possible

---

## Summary Table

| Category | Interfaces | Location | Purpose |
|----------|-----------|----------|---------|
| **Base** | IWord | Core/ | Foundation for all words |
| **Core Types** | IVerb, IVerb<>, INoun | Core/ | Main word classifications |
| **Objects** | IWhat<> | Nouns/ | Direct objects |
| **Prepositions** | IFrom<>, ITo<>, IWith<>, IUsing<> | Nouns/ | Grammatical relationships |
| **Validation** | IValidatable, ValidationResult, SentenceValidator | Validation/ | Syntax checking |
| **Metadata** | RequiredKeywordsAttribute | Attributes/ | Compile-time metadata |

---

**Last Updated:** October 19, 2025  
**Version:** 1.0  
**Maintainer:** FluNET Project Team
