# Canonical language reference

Canonical syntax is FluNET's explicit low-level command surface. It maps directly to the stable language snapshot, semantic frames, typed command compiler and execution planner. Compact syntax lowers into this same representation; canonical syntax therefore remains useful for precise ordering, compatibility and debugging.

## Core value forms

| Form | Meaning |
| --- | --- |
| `[name]` | Variable reference/output binding. |
| `{value}` | Structured/reference token such as a file path, URL or JSON value. |
| `"text"` / `'text'` | Quoted text literal. |
| `.` / `?` / `!` | Optional command terminators where syntactically unambiguous. |

Examples:

```text
SAY "Hello"
GET [lines] FROM {input.txt}.
LOAD CONFIG [config] FROM {settings.json}.
```

## Command connectors

### THEN

`THEN` creates an explicit sequence/control dependency:

```text
GET [lines] FROM {input.txt}
THEN
SAY [lines]
```

### AND

`AND` puts adjacent commands in the same parallel stage when their other dependencies permit it:

```text
GET [left] FROM {a.txt}
AND
GET [right] FROM {b.txt}
THEN
SAY [left] [right]
```

Inside a parenthesized `IF (...)`, `AND` is a Boolean expression operator rather than a command connector.

## Conditions

```text
SET BOOLEAN [enabled] TO true
THEN
SAY enabled IF [enabled]
ELSE
SAY disabled
```

Expression example:

```text
SAY ready IF ([enabled] AND NOT [blocked])
```

The expression grammar includes Boolean operators, comparisons, arithmetic, parentheses, property/index access and null coalescing in the shared expression AST.

## Execution policies

Retry:

```text
SAY hello WITH RETRY {2}
```

Timeout:

```text
SAY hello WITH TIMEOUT {5s}
```

Continue after command failure:

```text
SAY primary ON ERROR CONTINUE
THEN
SAY finished
```

Policies can be combined:

```text
SAY primary WITH RETRY {2} WITH TIMEOUT {5s} ON ERROR CONTINUE
THEN
SAY finished
```

## Built-in command families

The standard language snapshot currently contains typed frames for these families:

- `GET`
- `SAVE`
- `LOAD`
- `DELETE`
- `DOWNLOAD`
- `POST`
- `SAY`
- `SEND`
- `TRANSFORM`
- `SET`
- `PARSE`
- `FORMAT`

Common synonyms are defined by the language module (for example GET/FETCH/RETRIEVE, DOWNLOAD/GRAB/OBTAIN/PULL, SAY/ECHO/OUTPUT/PRINT/WRITE).

## File text

Read lines:

```text
GET [text] FROM {input.txt}.
```

Load text:

```text
LOAD TEXT [text] FROM {input.txt}.
```

Save text:

```text
SAVE [text] TO {copy.txt}.
```

## JSON/config

Load JSON/config:

```text
LOAD CONFIG [config] FROM {config.json}.
```

`JSON` is retained as a LOAD compatibility qualifier as well:

```text
LOAD JSON [config] FROM {config.json}.
```

Create typed JSON:

```text
SET JSON [config] TO {"enabled":true}.
```

Format JSON to text:

```text
SET JSON [config] TO {"enabled":true}
THEN
FORMAT JSON [pretty] FROM [config].
```

Parse text as JSON:

```text
PARSE JSON [document] FROM [text].
```

## Typed SET

Text:

```text
SET TEXT [name] TO Ada Lovelace.
```

Number:

```text
SET NUMBER [count] TO 42.
```

Boolean:

```text
SET BOOLEAN [enabled] TO true.
```

JSON:

```text
SET JSON [config] TO {"enabled":true}.
```

Typed literals are parsed during compilation. Invalid numbers/booleans/JSON are intended to fail before a handler executes.

## Download and HTTP mutation

Download:

```text
DOWNLOAD [file] FROM {https://example.com/file.txt} TO {file.txt}.
```

The standard language also has a typed JSON POST frame. Compact syntax provides the simpler authoring form:

```text
POST order TO https://api.example.com/orders
```

which lowers to the canonical POST frame.

## Variables and types

Canonical producer/consumer flow is checked by the typed compiler. Language types have stable `TypeId` identity and structural assignability; conversions such as Number -> Text are explicit value-registry edges rather than CLR casts.

The type system includes built-ins such as:

```text
Unit
Text
Boolean
Number
File
Directory
Uri
Json
Object
```

and structural types:

```text
List<T>
Map<K,V>
Optional<T>
Union
Object fields
```

## Canonical CLI

Run a prompt:

```bash
dotnet run --project src/FluNET.Cli -- -- "SAY 'Hello'."
```

Analyze without executing:

```bash
dotnet run --project src/FluNET.Cli -- --analyze -- "GET [text] FROM {input.txt}"
```

File roots:

```bash
dotnet run --project src/FluNET.Cli -- --root ./data -- "GET [text] FROM {./data/input.txt}."
```

Network allow-list:

```bash
dotnet run --project src/FluNET.Cli -- --host example.com -- "DOWNLOAD [file] FROM {https://example.com/a.txt} TO {a.txt}."
```

In canonical CLI mode, file access defaults to the current directory. If `--host` is omitted, the current CLI implementation leaves network access unrestricted; supplying one or more `--host` values restricts network access to those hosts.

## Canonical vs compact

Prefer canonical syntax when you need:

- explicit `AND`/`THEN` control staging;
- direct access to stable command/frame semantics;
- compatibility with existing host code;
- a debugging target for compact lowering.

Prefer compact syntax for normal authoring when the compiler can infer the obvious details. See [Compact language reference](compact-language.md).

## Compatibility layer

Historical sentence/token-tree APIs remain for source compatibility, but canonical execution uses the typed compiler/runtime. New modules should use native typed command declarations rather than deriving their semantic identity from legacy verb classes.

See [legacy API migration](legacy-api-migration.md) for migration details.