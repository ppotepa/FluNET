# DiscoveryService Cache Implementation

## Overview
Updated `DiscoveryService` to use static backing fields for caching discovered word types, improving performance and providing easy access to specific word categories.

## Implementation Details

### Static Backing Fields (Cache)
```csharp
private static IEnumerable<Type>? _allWords;
private static IEnumerable<Type>? _verbs;
private static IEnumerable<Type>? _nouns;
```

These fields are:
- **Static**: Shared across all instances of `DiscoveryService`
- **Nullable**: Can be cleared and re-initialized
- **Private**: Encapsulated, accessed only through properties

### Cache Initialization

Discovery happens once on the first instantiation:

```csharp
public DiscoveryService()
{
    if (_allWords == null)
    {
        _allWords = AppDomain
            .CurrentDomain
            .GetAssemblies()
            .SelectMany(x => x.GetTypes())
            .Where(x => typeof(IWord).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)
            .ToList(); // Materialize immediately to avoid re-evaluation
    }
}
```

### Public Properties

#### 1. Words Property
```csharp
public IEnumerable<Type> Words
{
    get
    {
        if (_allWords == null)
        {
            throw new InvalidOperationException("DiscoveryService has not been initialized.");
        }
        return _allWords;
    }
}
```
Returns all discovered word types (both verbs and nouns).

#### 2. Verbs Property (NEW)
```csharp
public IEnumerable<Type> Verbs
{
    get
    {
        if (_verbs == null)
        {
            _verbs = Words
                .Where(x => typeof(IVerb).IsAssignableFrom(x))
                .ToList();
        }
        return _verbs;
    }
}
```
Returns only verb types, cached after first access.

#### 3. Nouns Property (NEW)
```csharp
public IEnumerable<Type> Nouns
{
    get
    {
        if (_nouns == null)
        {
            _nouns = Words
                .Where(x => typeof(INoun).IsAssignableFrom(x))
                .ToList();
        }
        return _nouns;
    }
}
```
Returns only noun types, cached after first access.

### Cache Management

#### Clear Cache Method
```csharp
public static void ClearCache()
{
    _allWords = null;
    _verbs = null;
    _nouns = null;
}
```
Allows manual cache invalidation, useful for:
- Testing
- Dynamic assembly loading
- Plugin scenarios

## Usage Examples

### Basic Usage
```csharp
var discovery = new DiscoveryService();

// Get all verbs (cached after first call)
var verbs = discovery.Verbs;
Console.WriteLine($"Found {verbs.Count()} verb types");

// Get all nouns (cached after first call)
var nouns = discovery.Nouns;
Console.WriteLine($"Found {nouns.Count()} noun types");

// Get everything
var allWords = discovery.Words;
Console.WriteLine($"Total words: {allWords.Count()}");
```

### In Engine
```csharp
public TokenTree Run(ProcessedPrompt prompt)
{
    var tree = tokenTreeFactory.Process(prompt);
    
    // Access cached verb types
    var availableVerbs = discovery.Verbs;
    
    // Access cached noun types
    var availableNouns = discovery.Nouns;
    
    // Use for interpretation/validation
    // ...
    
    return tree;
}
```

### Cache Invalidation
```csharp
// Clear cache (e.g., after loading new assemblies)
DiscoveryService.ClearCache();

// Next instantiation will rediscover
var discovery = new DiscoveryService();
var verbs = discovery.Verbs; // Fresh discovery
```

## Performance Benefits

### Before (No Cache)
- Discovery runs on **every** access to `Words`
- Reflection operations repeated unnecessarily
- LINQ queries executed multiple times

### After (With Cache)
- Discovery runs **once** on first instantiation
- Results materialized with `.ToList()`
- Subsequent accesses return cached data
- Separate caches for Verbs and Nouns
- Zero reflection overhead after initialization

## Architecture Benefits

1. **Performance**: Reflection happens once, results cached
2. **Convenience**: Direct access to verb/noun types via properties
3. **Flexibility**: Can clear and refresh cache when needed
4. **Lazy Evaluation**: Verb/Noun caches populated on first access
5. **Thread-Safety**: Static fields shared across instances
6. **Testability**: Cache can be cleared between tests

## Example Output

```csharp
var discovery = new DiscoveryService();

// First call - performs discovery
var verbs = discovery.Verbs;
// Results: [GetText, GetBytes, ...]

// Second call - returns cached results (fast!)
var verbsAgain = discovery.Verbs;
// Results: Same list, no reflection

// Access nouns
var nouns = discovery.Nouns;
// Results: All types implementing INoun
```

## Next Steps

The cached verb types can be used for:
1. **Token Interpretation**: Match tokens to verb types
2. **Command Validation**: Check if a verb exists before execution
3. **Autocomplete**: Suggest available verbs to users
4. **Dynamic Instantiation**: Create verb instances based on user input
5. **Metadata Extraction**: Read attributes from verb types for help text
