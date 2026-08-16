# FluNET Classic Language

FluNET Classic is a sentence-oriented, typed and extensible scripting language hosted by .NET.

The compatibility baseline is commit `55455b46ed84fb447a8d47ba89772538944eb7b3` from `ppotepa/FluNET`.

## Compatibility rule

Existing Classic sentences remain valid as the implementation evolves. The runtime may be replaced by a parser, binder and richer execution model, but the established surface language is versioned as a public contract.

```text
GET [text] FROM file.txt
SAVE [content] TO output.txt
DOWNLOAD [data] FROM https://api.example.com
```

## Sentence model

A program is made from sentences. A sentence is a verb followed by typed semantic clauses.

```text
VERB WHAT<T> FROM<T> TO<T> USING<T> WITH<T>
```

Not every verb uses every role. For example:

```text
GET  WHAT<string> FROM<FileInfo>
SAVE WHAT<string> TO<FileInfo>
SEND WHAT<MailMessage> TO<MailAddress> USING<ITransport>
```

The CLR remains the underlying type system. FluNET language types are friendly projections of CLR types.

## Pipeline

`THEN` is the composition operator. The output of one sentence becomes the implicit input of the next when the sentence pattern allows it.

```text
GET JSON FROM api
THEN FILTER WHERE active
THEN SAVE JSON TO users.json
```

The explicit Classic form remains supported:

```text
GET JSON [users] FROM api
THEN FILTER [users] WHERE active AS [activeUsers]
THEN SAVE JSON [activeUsers] TO users.json
```

## Planned syntax families

The language evolves through orthogonal vocabulary rather than verb explosion:

- qualifiers: `TEXT`, `JSON`, `XML`, `BINARY`, `CSV`, ...
- clauses: `FROM`, `TO`, `USING`, `WITH`, `AS`, `WHERE`, ...
- composition: `THEN`
- expressions: variables, references, property access and interpolation
- control sentences: `IF ... THEN ... ELSE ...`, `FOR EACH ... IN ... THEN ...`

Extensions should contribute vocabulary and CLR-backed behavior through language modules rather than patching the engine.
