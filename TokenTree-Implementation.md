# TokenTree Implementation - Linked List Structure

## Overview
The TokenTree is now implemented as a doubly-linked list where each token points to its next and previous tokens.

## Key Components

### 1. Token Class (`Token/RawToken.cs`)
- **Properties:**
  - `Value`: The string value of the token
  - `Type`: TokenType enum (Root, Regular, Terminal)
  - `Next`: Reference to the next token in the list
  - `Previous`: Reference to the previous token in the list

- **Factory Methods:**
  - `CreateRoot()`: Creates the ROOT token (start of the list)
  - `CreateTerminal()`: Creates the TERMINAL token (end of the list)

### 2. TokenTree Class (`TokenTree/TokenTree.cs`)
- **Structure:**
  - Always starts with a `Root` token
  - Always ends with a `Terminal` token
  - Regular tokens are inserted between Root and Terminal

- **Properties:**
  - `Root`: The first token (always TokenType.Root)
  - `Terminal`: The last token (always TokenType.Terminal)
  - `Count`: Total number of tokens (including Root and Terminal)

- **Methods:**
  - `AddToken(token)`: Inserts a token before Terminal
  - `GetTokens()`: Returns an enumerable for forward traversal

### 3. TokenTreeFactory Class (`TokenTree/TokenTreeFactory.cs`)
- **Method:**
  - `Process(ProcessedPrompt)`: Creates a TokenTree from a prompt
    - Splits the prompt by spaces
    - Creates tokens via TokenFactory
    - Links them in a doubly-linked list
    - Returns the complete TokenTree

## Linked List Structure

```
ROOT ←→ Token1 ←→ Token2 ←→ Token3 ←→ ... ←→ TokenN ←→ TERMINAL
```

- Each token has bidirectional links (Next and Previous)
- Can traverse forward (Root → Terminal) using `Next`
- Can traverse backward (Terminal → Root) using `Previous`

## Example Output

For the prompt: "GET [DATA] FROM https://api.example.com/data AND SAVE TO [file.txt]"

### Forward Traversal (Root → Terminal):
```
[Root] ROOT
[Regular] GET
[Regular] [DATA]
[Regular] FROM
[Regular] https://api.example.com/data
[Regular] AND
[Regular] SAVE
[Regular] TO
[Regular] [file.txt]
[Terminal] TERMINAL
```

### Backward Traversal (Terminal → Root):
```
[Terminal] TERMINAL
[Regular] [file.txt]
[Regular] TO
[Regular] SAVE
[Regular] AND
[Regular] https://api.example.com/data
[Regular] FROM
[Regular] [DATA]
[Regular] GET
[Root] ROOT
```

## Usage

```csharp
var factory = new TokenTreeFactory(new TokenFactory());
var prompt = new ProcessedPrompt("your command here");
var tree = factory.Process(prompt);

// Access the root
var root = tree.Root;

// Traverse forward
var current = tree.Root;
while (current != null)
{
    Console.WriteLine(current.Value);
    current = current.Next;
}

// Traverse backward
current = tree.Terminal;
while (current != null)
{
    Console.WriteLine(current.Value);
    current = current.Previous;
}
```

## Benefits

1. **Bidirectional Navigation**: Can move forward and backward through tokens
2. **Clear Start/End**: Root and Terminal tokens mark boundaries
3. **Easy Insertion**: New tokens can be easily inserted anywhere in the list
4. **Efficient Traversal**: O(n) for complete traversal, O(1) for adding at end
5. **Type Safety**: TokenType enum distinguishes special tokens from regular ones
