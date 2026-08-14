# Compact language reference

Compact syntax is the inference-oriented front end of FluNET. It is parsed into `SurfaceProgramSyntax`, transformed by compile-time passes, lowered to canonical `PromptSyntax`, then compiled and executed by the same typed runtime as canonical programs.

This reference describes the current source tree, not a released language-version promise. Public `StandardLanguageIdentity.Version` remains `0.3` until the exact tree passes the Release verification gate.

## General rules

Compact source is line-oriented and indentation-aware.

```text
# comments start with # after indentation
LOAD config.json
SAY "{config.name}"
```

Blocks are introduced by supported block statements such as `FROM`, `FOR EACH`, `POLICY`, `WITH` and `TASK`.

Aliases use `AS`:

```text
GET https://api.example.test/posts AS posts
```

Top-level commas separate multiple values/resources where the command supports them:

```text
LOAD post.json, todo.json
```

A top-level `|` creates an explicit pipeline:

```text
GET https://api.example.test/posts AS posts | FILTER userId == 1 | TAKE 10
```

## SAY

```text
SAY "Hello"
SAY post.title
SAY "{post.title} — {todo.title}"
```

A bare property/index path such as `post.title` is lowered to the same interpolation mechanism used by quoted templates.

Supported path forms include:

```text
post.title
user.address.city
posts[0].title
```

## LOAD

Local JSON:

```text
LOAD config.json
LOAD config.json AS config
```

Local text:

```text
LOAD notes.txt AS notes
```

Multiple independent files:

```text
LOAD post.json, todo.json
```

JSON glob:

```text
LOAD data/*.json AS posts
```

Current built-in decoder limits:

| Inferred format | Compact built-in file read |
| --- | --- |
| JSON | yes |
| text (`.txt`, `.md`, `.log`) | yes |
| JSON glob | yes -> `List<Json>` |
| CSV | extension is recognized, decoder not built in |
| XML | extension is recognized, decoder not built in |
| binary/image | recognized by inference, no compact value decoder |

`LOAD` currently belongs to local files. Use `GET` for HTTP, environment and secrets.

## GET

File:

```text
GET config.json AS config
```

HTTP JSON:

```text
GET https://api.example.test/posts/1 AS post
```

Environment:

```text
GET env:DATABASE_URL AS database_url
```

Secret:

```text
GET secret:github-token AS token
```

The default secret policy denies reads. See [Secrets](#secrets).

The built-in compact HTTP provider currently expects JSON. HTTP resources inferred as another format are rejected instead of silently decoding them as something else.

## Resource contexts

Named base:

```text
USE https://api.example.test AS api
GET api/posts AS posts
GET api/users AS users
```

Lexical base:

```text
FROM https://api.example.test
    GET posts AS posts
    GET users AS users
```

The `FROM` base is lexical compile-time context. Relative GET resources in that block are resolved against it.

## Reliability directives

Within a lexical context you can set retry/timeout defaults:

```text
FROM https://api.example.test
    RETRY 3
    TIMEOUT 10s
    GET posts AS posts
```

These directives are lowered to the normal canonical execution policy. Supported timeout suffixes in the execution planner include milliseconds/seconds/minutes/hours.

`AUTH` is reserved in compact lowering and currently reports a diagnostic. Secret providers exist, but there is not yet a general compact `AUTH` application contract.

## SAVE

```text
SAVE value TO output.txt
SAVE post TO post.json
```

Simple identifiers are treated as variable references. The target is a file path/reference consumed by the canonical SAVE frame.

## POST

```text
POST order TO https://api.example.test/orders
```

Compact POST currently targets an absolute HTTP(S) URI and lowers to the canonical JSON POST frame.

Idempotent mutation:

```text
POST order TO https://api.example.test/orders ONCE BY order.id
```

See [ONCE BY](#once-by).

## Automatic data dependencies

Compact source normally relies on dataflow instead of explicit `AND`/`THEN`:

```text
LOAD post.json, todo.json
SAY "{post.title} — {todo.title}"
```

The dependency analyzer finds producer/consumer edges and effect metadata. Independent `Read`/`Pure` work can run concurrently; ordered/effectful operations remain conservative.

Canonical `AND` and `THEN` remain available in the canonical language when explicit ordering is required.

## Pipelines

Explicit:

```text
GET https://api.example.test/posts AS posts | FILTER userId == 1 | SORT BY title | TAKE 10
```

Implicit multiline flow:

```text
GET https://api.example.test/posts AS posts
FILTER userId == 1
SORT BY title
TAKE 10
```

The compiler uses normal synthetic variables between stages. There is no ambient global `$it` runtime variable.

## FILTER

```text
FILTER userId == 1
FILTER published AND score >= 10
```

The predicate uses the shared expression grammar and evaluates identifiers against the current JSON row.

## SORT

```text
SORT BY title
SORT BY score
```

Sorting is stable for equal keys.

## TAKE

```text
TAKE 10
```

Count must be a non-negative integer.

## SELECT

```text
SELECT id, title
SELECT id, userId, title
```

Each output row contains only the selected fields/expressions.

## MAP

```text
MAP TO { id, headline: title, owner: userId }
```

MAP and SELECT share the same JSON projection runtime.

## DEFAULT

```text
DEFAULT title TO "untitled"
DEFAULT count TO 0
```

Current DEFAULT updates a **top-level JSON field** when that field is absent or null. It is not a general nested l-value assignment syntax.

## Expressions

The shared expression AST supports:

- `OR`, `AND`, `NOT` and `!`;
- `==`, `!=`, `<`, `<=`, `>`, `>=`;
- `+`, `-`, `*`, `/`;
- unary `-`;
- `??` null coalescing;
- parentheses;
- property access and indexing;
- `LIST(...)` and `OBJECT(...)` nodes in the core expression parser.

Examples:

```text
FILTER userId == 1 AND score * 2 >= 8
DEFAULT port TO config.port ?? 8080
```

## GROUP

Implicit input:

```text
GROUP BY customerId AS grouped
```

Explicit collection:

```text
GROUP orders BY customerId AS grouped
```

## SUM

```text
SUM total AS revenue
```

The expression must evaluate numerically for each row.

## JOIN

```text
JOIN posts WITH users ON posts.userId = users.id AS enriched
```

`==` is also accepted. JOIN combines two already-produced JSON collections; it does not perform I/O.

## MATCH

```text
MATCH posts.userId TO users.id AS enriched
```

MATCH is a compact join form over collection paths.

## FOR EACH

```text
GET https://api.example.test/users AS users
FOR EACH user
    SAY "{user.name}"
```

Bounded concurrency:

```text
FOR EACH user PARALLEL 8
    SAY "{user.name}"
```

Current contract:

- default concurrency: 4;
- explicit `PARALLEL`: 1..256;
- body must contain at least one action;
- **body currently supports `SAY` actions only**.

The compiler deliberately rejects arbitrary GET/POST/enrichment inside the loop until a general iteration action/provider contract exists.

## TASK and RUN

Definition:

```text
TASK fetch-user id -> Json
    GET https://api.example.test/users/{id} AS user
```

Call:

```text
RUN fetch-user 42 AS user
SAY "{user.name}"
```

Header shape:

```text
TASK name [parameter ...] [-> Type]
```

Invocation shape:

```text
RUN name [argument ...] [AS result]
```

Semantics:

- parameter names must be unique identifiers;
- a declared result type must exist in the active language type system;
- parameters substitute only explicit `{parameter}` placeholders;
- local aliases are hygienically renamed;
- `AS result` renames the final value-producing statement;
- cycles/recursive expansion are rejected; expansion depth is bounded at 32.

## POLICY and WITH

```text
POLICY resilient
    RETRY 3
    TIMEOUT 30s
    CONTINUE ON ERROR

WITH resilient
    GET https://api.example.test/posts AS posts
    GET https://api.example.test/users AS users
```

Policy body currently supports:

- `RETRY n`, 0..100;
- `TIMEOUT duration`;
- `CONTINUE` or `CONTINUE ON ERROR`.

Not implemented in the current policy contract:

- `BACKOFF ...`;
- status-specific `CONTINUE ON 404`.

A command may also carry `USING profile` in its final compact value, but block-form `WITH profile` is easier to read and is preferred in documentation.

## CACHE

```text
GET https://api.example.test/catalog CACHE 1h AS catalog
```

Put `CACHE duration` **before `AS alias`**.

Rules:

- duration suffixes: `ms`, `s`, `m`, `h`, `d`;
- duration must be positive and at most 365 days;
- only `Read` or `Pure` commands can be cached;
- current cache keys require literal/resource inputs; commands whose cache input contains variables are rejected;
- default `IExecutionResultCache` is in-memory/process-local.

## ONCE BY

```text
POST order TO https://api.example.test/orders ONCE BY order.id
```

Rules:

- valid only for `Write` or `ExternalMutation` effects;
- key may be a dynamic path such as `order.id` or a literal;
- command fingerprint is included in the final idempotency key;
- default `IIdempotencyStore` is in-memory/process-local.

## Secrets

```text
GET secret:github-token AS token
```

`SecretValue` is opaque:

- `ToString()` produces `<secret>`;
- revealing plaintext is an explicit host operation;
- default secret store is empty;
- default access policy denies all secret reads;
- there is no implicit Secret -> Text conversion.

Embedding hosts can install `DictionarySecretStore` (or their own store) together with `AllowListedSecretAccessPolicy`.

## Automation syntax is separate

`EVERY`, `WATCH`, `WHEN` are compiled by `AutomationCompiler`, not by normal `SurfaceCompiler`/`flunet run`.

See [Automation and desired state](automation-and-desired-state.md).

## ENSURE syntax is separate

`ENSURE` is compiled by `EnsureCompiler` and executed through the embedding API.

See [Automation and desired state](automation-and-desired-state.md).

## Not a current compact feature

The current `main` does **not** expose a supported public compact implementation for:

- SQL resource execution (no built-in SQL provider);
- generic `AUTH` application;
- CSV/XML compact decoding;
- arbitrary actions inside `FOR EACH`;
- `SYNC`/general reconciliation;
- generic compensation language syntax;
- distributed workflow coordination.

See [Status and limitations](status-and-limitations.md) for the authoritative support matrix.