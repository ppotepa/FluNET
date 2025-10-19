# FluNET Verb Implementations

This document provides a comprehensive overview of all implemented verbs in the FluNET language.

## Overview

FluNET currently implements **7 verb types** with concrete implementations for various use cases. Each verb follows specific grammatical patterns and accepts particular prepositions.

---

## Verb Catalog

### 1. GET Verb

**Pattern:** `GET [what] FROM [source]`

**Purpose:** Retrieve data from a source

**Type Parameters:**
- `TWhat`: Type of data being retrieved
- `TFrom`: Type of source

**Implementations:**
- **GetText** - Reads text from a file
  - Input: `FileInfo`
  - Output: `string[]` (lines)
  - Example: `GET [data] FROM [file.txt].`

**Validation Rules:**
- Must be followed by FROM preposition or direct object (IWhat)
- Sentence must end with valid terminator (., ?, !)

---

### 2. POST Verb

**Pattern:** `POST [data] TO [destination]`

**Purpose:** Send data to a destination endpoint

**Type Parameters:**
- `TWhat`: Type of data being sent
- `TTo`: Type of destination

**Implementations:**
- **PostJson** - Posts JSON data to HTTP endpoint
  - Input: `Uri`
  - Output: `string` (response)
  - Example: `POST [json] TO [https://api.example.com/endpoint].`

**Validation Rules:**
- Must be followed by TO preposition or direct object (IWhat)
- Sentence must end with valid terminator

**HTTP Example:**
```csharp
var verb = new PostJson("{\"key\":\"value\"}", new Uri("https://api.example.com"));
var response = verb.Act(new Uri("https://api.example.com"));
```

---

### 3. SAVE Verb

**Pattern:** `SAVE [data] TO [destination]`

**Purpose:** Store data persistently to a destination

**Type Parameters:**
- `TWhat`: Type of data being saved
- `TTo`: Type of storage destination

**Implementations:**
- **SaveText** - Writes text to a file
  - Input: `FileInfo`
  - Output: `string` (confirmation)
  - Example: `SAVE [document] TO [output.txt].`

**Validation Rules:**
- Must be followed by TO preposition or direct object (IWhat)
- Sentence must end with valid terminator

**File Writing Example:**
```csharp
var verb = new SaveText("Hello, World!", new FileInfo("output.txt"));
verb.Act(new FileInfo("output.txt")); // Writes to file
```

---

### 4. DELETE Verb

**Pattern:** `DELETE [resource] FROM [location]`

**Purpose:** Remove data from a location

**Type Parameters:**
- `TWhat`: Type of resource identifier
- `TFrom`: Type of location

**Implementations:**
- **DeleteFile** - Removes a file from a directory
  - Input: `DirectoryInfo`
  - Output: `string` (status message)
  - Example: `DELETE [temp.txt] FROM [C:\Temp].`

**Validation Rules:**
- Must be followed by FROM preposition or direct object (IWhat)
- Sentence must end with valid terminator

**File Deletion Example:**
```csharp
var verb = new DeleteFile("temp.txt", new DirectoryInfo(@"C:\Temp"));
var result = verb.Act(new DirectoryInfo(@"C:\Temp"));
```

---

### 5. LOAD Verb

**Pattern:** `LOAD [data] FROM [source]`

**Purpose:** Load data into memory or application state

**Type Parameters:**
- `TWhat`: Type of data being loaded
- `TFrom`: Type of source

**Implementations:**
- **LoadConfig** - Loads configuration from JSON file
  - Input: `FileInfo`
  - Output: `Dictionary<string, object>`
  - Example: `LOAD [config] FROM [settings.json].`

**Validation Rules:**
- Must be followed by FROM preposition or direct object (IWhat)
- Sentence must end with valid terminator

**Configuration Example:**
```csharp
var verb = new LoadConfig(new Dictionary<string, object>(), new FileInfo("settings.json"));
var config = verb.Act(new FileInfo("settings.json"));
```

---

### 6. SEND Verb

**Pattern:** `SEND [message] TO [recipient]`

**Purpose:** Transmit messages or data to a recipient

**Type Parameters:**
- `TWhat`: Type of message being sent
- `TTo`: Type of recipient

**Implementations:**
- **SendEmail** - Sends email messages
  - Input: `string` (email address)
  - Output: `string` (confirmation)
  - Example: `SEND [message] TO [user@example.com].`

**Validation Rules:**
- Must be followed by TO preposition or direct object (IWhat)
- Sentence must end with valid terminator

**Email Example:**
```csharp
var verb = new SendEmail("Hello!", "user@example.com");
var result = verb.Act("user@example.com");
```

---

### 7. TRANSFORM Verb

**Pattern:** `TRANSFORM [data] USING [method]`

**Purpose:** Convert or modify data using a specific method or algorithm

**Type Parameters:**
- `TWhat`: Type of data being transformed
- `TUsing`: Type of transformation method

**Implementations:**
- **TransformEncoding** - Encodes text using character encoding
  - Input: `System.Text.Encoding`
  - Output: `string` (base64 encoded)
  - Example: `TRANSFORM [text] USING [UTF8].`

**Validation Rules:**
- Must be followed by USING preposition or direct object (IWhat)
- Sentence must end with valid terminator

**Encoding Example:**
```csharp
var verb = new TransformEncoding("Hello", System.Text.Encoding.UTF8);
var encoded = verb.Act(System.Text.Encoding.UTF8);
```

---

## Verb Comparison Matrix

| Verb | Primary Preposition | Secondary Options | Use Case | Return Type |
|------|---------------------|-------------------|----------|-------------|
| GET | FROM | - | Data retrieval | TWhat |
| POST | TO | - | HTTP/API calls | TWhat |
| SAVE | TO | - | Persistent storage | TWhat |
| DELETE | FROM | - | Data removal | TWhat |
| LOAD | FROM | - | Memory loading | TWhat |
| SEND | TO | - | Message delivery | TWhat |
| TRANSFORM | USING | - | Data conversion | TWhat |

---

## Preposition Usage Patterns

### FROM Prepositions
Used to indicate source or origin:
- GET ... FROM
- DELETE ... FROM
- LOAD ... FROM

### TO Prepositions
Used to indicate destination:
- POST ... TO
- SAVE ... TO
- SEND ... TO

### USING Prepositions
Used to indicate method or tool:
- TRANSFORM ... USING

### WITH Prepositions
Available for future implementations:
- CONNECT ... WITH [credentials]
- ENCRYPT ... WITH [key]

---

## Creating New Verbs

### Step 1: Create Abstract Base Class

```csharp
public abstract class MyVerb<TWhat, TPrep> : IVerb<TWhat, TPrep>,
    IWhat<TWhat>,
    IMyPreposition<TPrep>
{
    protected MyVerb(TWhat what, TPrep prep)
    {
        What = what;
        MyPreposition = prep;
    }
    
    public TWhat What { get; protected set; }
    public TPrep MyPreposition { get; protected set; }
    public string Text => "MYVERB";
    public abstract Func<TPrep, TWhat> Act { get; }
    
    public IWord? Next { get; set; }
    public IWord? Previous { get; set; }
    
    public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon)
    {
        if (nextWord is IMyPreposition<TPrep>)
            return ValidationResult.Success();
            
        return nextWord is IWhat<TWhat>
            ? ValidationResult.Success()
            : ValidationResult.Failure("Expected MY_PREPOSITION or direct object");
    }
}
```

### Step 2: Create Concrete Implementation

```csharp
public class MyVerbConcrete : MyVerb<OutputType, InputType>
{
    public MyVerbConcrete(OutputType what, InputType prep) 
        : base(what, prep) { }
    
    public override Func<InputType, OutputType> Act => 
        (input) => {
            // Implement the action
            return result;
        };
}
```

### Step 3: Add Tests

```csharp
[Test]
public void MyVerb_WithPreposition_ShouldValidate()
{
    ProcessedPrompt processed = new("MYVERB data MYPREPOSITION value.");
    TokenTree tree = processed.ToTokenTree();
    ValidationResult result = validator.ValidateSentence(tree);
    
    if (!result.IsValid)
    {
        Assert.That(result.FailureReason, Does.Not.Contain("terminator"));
    }
}
```

---

## Test Coverage

### Current Test Statistics
- **Total Tests:** 50
- **Verb Tests:** 23
- **Structure Tests:** 22
- **Validation Tests:** 5
- **All Passing:** ✅ Yes

### Test Categories

#### Verb-Specific Tests
Each verb has dedicated tests for:
- Basic validation with primary preposition
- Terminator validation
- Question mark and exclamation support
- Multiple word patterns

#### Structure Tests
- Token counting accuracy
- Chain navigation
- ToString reconstruction
- Empty/whitespace handling
- Special character support
- URL and email handling

#### Validation Tests
- Terminator requirements
- Empty sentence detection
- Unknown verb handling
- Grammatical structure validation

---

## Usage Examples

### Simple Data Retrieval
```
GET user_data FROM database.
LOAD config FROM settings.json.
```

### Data Transmission
```
POST json_payload TO https://api.example.com/users.
SEND notification TO admin@example.com.
```

### Data Persistence
```
SAVE report TO output_file.txt.
DELETE temp_file FROM temp_directory.
```

### Data Transformation
```
TRANSFORM plaintext USING AES256_encryption.
TRANSFORM data USING UTF8_encoding.
```

### Complex Sentences
```
GET the user profile data FROM remote database server.
POST the processed analytics results TO cloud storage endpoint.
```

---

## Best Practices

### 1. Always Use Terminators
```
✅ CORRECT: GET data FROM file.
❌ WRONG: GET data FROM file
```

### 2. Match Type Parameters
```csharp
// TWhat and TFrom must match verb expectations
Get<string[], FileInfo> // Correct pairing
Get<int, string>        // May cause runtime issues
```

### 3. Implement Error Handling
```csharp
public override Func<FileInfo, string[]> Act
{
    get
    {
        return (file) =>
        {
            if (!file.Exists)
                throw new FileNotFoundException($"File not found: {file.Name}");
            
            return File.ReadAllLines(file.FullName);
        };
    }
}
```

### 4. Use Descriptive Names
```
✅ GOOD: SaveUserProfile, LoadConfiguration, TransformToJson
❌ BAD: Save1, Load2, Transform3
```

---

## Future Verb Ideas

### Planned Implementations
- **PUT** - Update existing resources
- **PATCH** - Partial updates
- **MERGE** - Combine data sources
- **FILTER** - Extract subset of data
- **MAP** - Transform collections
- **REDUCE** - Aggregate data
- **VALIDATE** - Check data integrity
- **ENCRYPT/DECRYPT** - Security operations
- **COMPRESS/DECOMPRESS** - Data compression
- **PARSE** - Data parsing operations

---

**Last Updated:** October 19, 2025  
**Version:** 2.0  
**Test Coverage:** 50 tests, 100% passing
