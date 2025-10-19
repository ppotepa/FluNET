# Future Usage Patterns

The Lexicon system is designed to be extensible with additional usage patterns beyond IWhat and IFrom.

## Current Usage Patterns

### IWhat<TWhat>
- Defines what is being operated on (the target object/data)
- Example: `Get<string[], FileInfo>` - What = string[]
- Property: `TWhat What { get; }`

### IFrom<TFrom>
- Defines the source of the operation
- Example: `Get<string[], FileInfo>` - From = FileInfo
- Property: `TFrom From { get; }`

## Future Usage Pattern Examples

### ITo<TTo>
- Defines the destination of an operation
- Example: `Save<byte[], FileInfo>` - To = FileInfo
- Property: `TTo To { get; }`
- Use case: SAVE data TO file

### IWith<TWith>
- Defines additional parameters or configuration
- Example: `Encrypt<string, EncryptionConfig>` - With = EncryptionConfig
- Property: `TWith With { get; }`
- Use case: ENCRYPT data WITH algorithm

### IUsing<TUsing>
- Defines a tool or method to use
- Example: `Transform<Data, Transformer>` - Using = Transformer
- Property: `TUsing Using { get; }`
- Use case: TRANSFORM data USING converter

### IAs<TAs>
- Defines a format or type conversion
- Example: `Export<Data, JsonFormat>` - As = JsonFormat
- Property: `TAs As { get; }`
- Use case: EXPORT data AS json

### IBy<TBy>
- Defines the agent or mechanism
- Example: `Sort<Collection, Comparer>` - By = Comparer
- Property: `TBy By { get; }`
- Use case: SORT list BY property

### IAt<TAt>
- Defines location or position
- Example: `Insert<Data, Position>` - At = Position
- Property: `TAt At { get; }`
- Use case: INSERT item AT position

### IFor<TFor>
- Defines purpose or duration
- Example: `Cache<Data, TimeSpan>` - For = TimeSpan
- Property: `TFor For { get; }`
- Use case: CACHE data FOR duration

## Implementation Notes

When adding new usage patterns:

1. **Create the interface** (e.g., ITo<TTo>) with appropriate property
2. **Lexicon automatically discovers** generic parameters via reflection
3. **Type checking happens automatically** when building the Lexicon dictionary
4. **Validation uses** the discovered types for FROM/WHAT/TO/etc. validation

Example Command Patterns:
```
GET Text FROM file.txt
SAVE Data TO output.json
ENCRYPT message WITH AES256
TRANSFORM data USING mapper
EXPORT records AS csv
SORT items BY name
INSERT item AT position
CACHE result FOR 5minutes
```

The Lexicon extracts type information from generic parameters, so:
- `Get<string[], FileInfo>` → FromType = FileInfo, WhatType = string[]
- `Save<byte[], FileInfo>` → ToType = FileInfo, WhatType = byte[]
- `Encrypt<string, EncryptionConfig>` → WithType = EncryptionConfig, WhatType = string

This enables compile-time type safety and runtime validation of command parameters.
