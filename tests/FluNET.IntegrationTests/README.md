# FluNET Integration Tests

## Overview
This project contains integration tests for FluNET with the new `{reference}` and `[variable]` syntax. These tests are isolated from legacy tests to ensure clean execution without cross-contamination.

## Test Coverage

### Tokenization Tests (13 tests)
- ✅ Simple reference tokenization: `{C:\test.txt}`
- ✅ References with spaces: `{C:\Test Files\document.txt}`
- ✅ Nested braces: `{{{filepath}}}`
- ✅ Variables: `[myVariable]`
- ✅ Multiple references in one sentence
- ✅ URL references: `{https://example.com/api/data}`
- ✅ Empty braces and brackets
- ✅ Multiple spaces between tokens
- ✅ Complex paths with multiple spaces
- ✅ Nested variable in reference

### GET Command Integration Tests (10 tests)
- ✅ Reading from existing file
- ✅ Reading from non-existent file (returns null)
- ✅ Variable resolution in FROM clause
- ✅ Relative path handling
- ✅ Multiple executions
- ✅ Empty file handling
- ✅ Large file handling (1000+ lines)
- ✅ Special characters in paths
- ✅ Paths with spaces
- ✅ Nested braces in references
- ✅ Trailing period handling

## New Syntax

### Reference Syntax
References to external resources (files, URLs, etc.) use curly braces:
```
GET [text] FROM {C:\path\to\file.txt} .
GET [data] FROM {https://api.example.com/data} .
GET [content] FROM {file with spaces.txt} .
```

### Variable Syntax
Variables use square brackets:
```
GET [myVariable] FROM {source.txt} .
```

### Nested Braces
Support for nested braces for template variables:
```
GET [text] FROM {{{variableName}}} .
```

## Implementation Details

### Tokenization
- **Depth-aware parsing**: Uses counter-based tracking instead of boolean flags
- **Preserves spaces**: Spaces inside `{reference}` and `[variable]` are preserved
- **Handles nesting**: Correctly processes nested braces like `{{{path}}}`

### DI Scope Management
- Proper `IServiceScope` creation for each test
- All services registered as `Scoped` instead of `Singleton`
- Scope and ServiceProvider properly disposed in teardown

### Test Isolation
- Each test creates its own temporary directory with GUID
- Each test has independent DI scope
- No shared state between tests

## Running Tests

### Run all integration tests:
```powershell
dotnet test tests/FluNET.IntegrationTests/FluNET.IntegrationTests.csproj
```

### Run specific test:
```powershell
dotnet test tests/FluNET.IntegrationTests/FluNET.IntegrationTests.csproj --filter "Get_FromExistingFile_ShouldReturnFileContents"
```

## Test Results
- **Total Tests**: 23
- **Passed**: 23 (100%)
- **Failed**: 0
- **Duration**: ~3.7s

## Key Features Validated

✅ **Brace-Aware Tokenization**
- Correctly handles `{path with spaces.txt}` as single token
- Supports nested braces `{{{variable}}}`
- Preserves URL structure `{https://example.com/api}`

✅ **ReferenceWord Implementation**
- First-class word type with `Reference` property
- Resolution to FileInfo, Uri, or string
- Verb validation accepts ReferenceWord

✅ **Variable Resolution**
- Variables can be registered with `engine.RegisterVariable(name, value)`
- Variables are resolved during execution
- Support for both `[variable]` and `{reference}` in same sentence

✅ **File System Integration**
- Reads from existing files
- Returns null for non-existent files
- Handles empty files correctly
- Processes large files (1000+ lines)
- Works with relative and absolute paths

## Architecture

### Token Processing Pipeline
1. **ProcessedPrompt** → Tokenizes with `TokenizeWithBraceAwareness()`
2. **TokenTreeFactory** → Uses `prompt.Tokens` (not naive split)
3. **TokenFactory** → Identifies `TokenType.Reference` and `TokenType.Variable`
4. **WordFactory** → Creates `ReferenceWord` or `VariableWord` instances
5. **SentenceValidator** → Validates sentence structure
6. **SentenceExecutor** → Executes with proper variable/reference resolution

### Critical Implementation
- **ProcessedPrompt.cs**: `TokenizeWithBraceAwareness()` uses depth counters
- **TokenTreeFactory.cs**: Uses `prompt.Tokens` instead of splitting
- **ReferenceWord.cs**: Strips braces and provides `ResolveAs<T>()` method
- **GetText.cs**: Updated `Validate()` to accept ReferenceWord

## Notes
- Tests use .NET 10.0 (latest preview)
- NUnit 5.0.0.0 test framework
- Microsoft.Extensions.DependencyInjection 9.0.10
- All diagnostics show correct token types in console output
