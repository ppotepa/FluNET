# Compact separators: `.`, `,`, `;`, `|`, `AND`, `THEN`

FluNET uses different separators for different kinds of relationships. The key rule is that punctuation should describe **syntax/dataflow**, not accidentally serialize work.

## Quick reference

| Form | Meaning | Creates execution ordering? |
| --- | --- | --- |
| `,` | another element of the same syntactic role | no |
| `;` | another statement in the same lexical scope | no |
| `.` | spoken sentence terminator, equivalent to a newline | no |
| newline | another statement in the same lexical scope | no |
| `|` | feed the produced value into the next pipeline stage | yes, through data dependency |
| canonical `AND` | explicit parallel coordination | explicit same-stage relationship |
| canonical `THEN` | explicit ordering/barrier | yes |

## Comma: “more of the same”

A top-level comma coordinates values that belong to one command or one expression construct.

```text
LOAD post.json, todo.json
SELECT id, title, user.name
LIST(1, 2, 3)
```

For resource reads, the comma does **not** itself mean “run these in parallel”. It means “these resources belong to the same LOAD/GET statement”. The dependency/effect analyzer decides whether the resulting operations are safe to schedule concurrently.

Commas inside quotes or nested `()`, `[]` and `{}` are not top-level value separators.

## Semicolon: neutral statement boundary

A top-level semicolon has the same compact-language meaning as a newline:

```text
LOAD post.json, todo.json; SAY "{post.title} — {todo.title}"
```

is equivalent to:

```text
LOAD post.json, todo.json
SAY "{post.title} — {todo.title}"
```

The semicolon does **not** mean `THEN`, `AND` or `|`.

For example:

```text
GET https://api.example.test/posts AS posts;
GET https://api.example.test/users AS users
```

contains two independent read statements. Because neither consumes the other's result and both are read operations, the dependency graph can schedule them together.

By contrast:

```text
GET https://api.example.test/posts AS posts; SAY "{posts[0].title}"
```

still creates a dependency from GET to SAY because the interpolation reads `posts`.

## Trailing semicolon

A final semicolon is a legal statement terminator:

```text
SAY "done";
```

Leading or interior empty statements are rejected:

```text
; SAY "invalid"
SAY "one";; SAY "two"
```

They produce `FLN218`.

## Quotes and nested structures protect semicolons

Only a **top-level** `;` is a statement separator.

These remain one statement/value:

```text
SAY "alpha; beta"
SAY {left;right}
```

The splitter tracks quote state and delimiter depth before treating `;` as punctuation.

## Period: a spoken sentence terminator

A top-level period followed by whitespace or the end of input separates
sentences. Periods inside quotes and nested values remain data:

```text
GET users FROM https://api.example.test/users. COUNT users AS total.
SAY "A period is data here."
```

As with a newline, a period does not imply ordering; data dependencies and
explicit `THEN` still determine execution order. Quote a value whose final
character is a literal period.

If an unquoted resource literally needs a top-level semicolon as part of its value (for example an unusual URI path), quote the value so the semicolon is data rather than syntax.

## Semicolons inside blocks

A semicolon can separate ordinary statements that already belong to the same indentation scope:

```text
FROM https://api.example.test
    GET posts AS posts; GET users AS users
```

It does not replace indentation for block ownership. This is intentionally invalid as an inline block:

```text
FROM https://api.example.test; GET posts
```

`FROM`, `FOR EACH`, `POLICY`, `WITH` and `TASK` still use indentation-aware blocks.

## Formatter behavior

`flunet fmt` normalizes semicolon-separated statements to the ordinary multiline representation:

Input:

```text
LOAD post.json, todo.json; SAY "{post.title} — {todo.title}";
```

Formatted output:

```text
LOAD post.json, todo.json
SAY "{post.title} — {todo.title}"
```

The AST does not need a separate runtime concept for semicolon versus newline; both are neutral statement boundaries.

## Why semicolon is not `THEN`

Making `;` equivalent to `THEN` would unnecessarily serialize independent work:

```text
GET posts; GET users
```

would become an artificial sequence even though no data dependency exists. Compact FluNET instead follows the rule:

> source order separates statements; dependencies and effects determine safe execution order.

Use canonical `THEN` when explicit ordering is part of the intent.

## Why comma is not `AND`

Likewise:

```text
LOAD a.json, b.json
```

means “load these resources”, not “force an explicit parallel branch”. The compiler is free to derive parallel execution when the operations are independent and their effect metadata permits it.
