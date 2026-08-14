# Getting started: from one command to workflows

This guide uses the **compact syntax** unless a section explicitly says canonical. Compact source is meant to remove syntax that the compiler can infer safely: file formats, obvious output names and data dependencies.

> Status: the public built-in language version is still `0.3`; the compact/data/automation layers described here are source-level experimental APIs on `main` and still require a full Release build/test verification before a release claim.

## 1. Print text

Create `hello.fln`:

```text
SAY "Hello from FluNET"
```

Run it:

```bash
dotnet run --project src/FluNET.Cli -- run hello.fln
```

You can also use the canonical prompt runner directly:

```bash
dotnet run --project src/FluNET.Cli -- -- "SAY 'Hello from canonical FluNET'."
```

## 2. Load a file and use the inferred variable

```text
LOAD settings.json
SAY "Environment: {settings.environment}"
```

For a plain local filename FluNET infers:

- local-file resource;
- format from the extension (`.json` or a supported text extension);
- output name from the filename (`settings.json` -> `settings`);
- dependencies from later references to that output.

You can always override the name:

```text
LOAD settings.json AS config
SAY "Environment: {config.environment}"
```

Supported built-in compact file reads currently have executable decoders for JSON and text. The inference layer recognizes more extensions, but CSV/XML/binary/image decoding is not yet provided by the built-in file provider.

## 3. Load independent inputs in parallel

```text
LOAD post.json, todo.json
SAY "{post.title} — {todo.title}"
```

The two file reads are independent `Read` effects, so the dependency analyzer can schedule them together. `SAY` refers to both outputs and therefore waits for both. You do not need to write `AND` and `THEN` in compact source for this simple dataflow.

The equivalent low-level idea is roughly:

```text
LOAD CONFIG [post] FROM {post.json}
AND
LOAD CONFIG [todo] FROM {todo.json}
THEN
SAY "{post.title} — {todo.title}"
```

Canonical syntax remains available when you want explicit control edges.

## 4. GET local, HTTP and environment resources

Local file:

```text
GET settings.json AS settings
```

HTTP JSON:

```text
GET https://jsonplaceholder.typicode.com/posts/1 AS post
SAY "{post.title}"
```

Environment value:

```text
GET env:HOME AS home
SAY "Home: {home}"
```

The built-in HTTP compact provider currently has a JSON contract. It is not a generic content-type negotiation layer yet.

## 5. Reuse an API base

Named base:

```text
USE https://jsonplaceholder.typicode.com AS api
GET api/posts/1 AS post
GET api/todos/1 AS todo
SAY "{post.title} — {todo.title}"
```

Lexical block:

```text
FROM https://jsonplaceholder.typicode.com
    GET posts/1 AS post
    GET todos/1 AS todo
    SAY "{post.title} — {todo.title}"
```

`FROM` is a compile-time context. It does not mutate a global runtime URL.

## 6. Inspect what the compiler inferred

```bash
dotnet run --project src/FluNET.Cli -- check program.fln
dotnet run --project src/FluNET.Cli -- explain program.fln
dotnet run --project src/FluNET.Cli -- graph program.fln
dotnet run --project src/FluNET.Cli -- fmt program.fln
```

- `check` compiles without effects;
- `explain` shows inference/lowering/plan information;
- `graph` emits Graphviz DOT for the dependency DAG;
- `fmt` formats compact source to stdout.

The surface tool commands use the current working directory as their file root and currently leave network access unrestricted. Canonical prompt mode has explicit `--root` and `--host` options.

## 7. Build a data pipeline

```text
GET https://jsonplaceholder.typicode.com/posts AS posts
FILTER userId == 1
SORT BY title
TAKE 10
```

The same flow can be written explicitly:

```text
GET https://jsonplaceholder.typicode.com/posts AS posts | FILTER userId == 1 | SORT BY title | TAKE 10
```

Pipeline stages use normal typed variables internally; there is no special global `$it` runtime value.

## 8. Select and reshape data

Select fields:

```text
GET https://jsonplaceholder.typicode.com/posts AS posts
SELECT id, title
```

Map to a new JSON shape:

```text
GET https://jsonplaceholder.typicode.com/posts AS posts
MAP TO { id, headline: title, owner: userId }
```

Fill missing/null top-level fields:

```text
DEFAULT title TO "untitled"
```

Expressions share the same precedence model and include Boolean operators, comparisons, arithmetic, property/index access and `??` coalescing.

## 9. Aggregate and combine collections

Group:

```text
GET https://api.example.test/orders AS orders
GROUP BY customerId AS byCustomer
```

Sum:

```text
GET https://api.example.test/orders AS orders
SUM total AS revenue
```

Join two named collections:

```text
GET https://api.example.test/posts AS posts
GET https://api.example.test/users AS users
JOIN posts WITH users ON posts.userId = users.id AS enriched
```

Equivalent compact match form:

```text
MATCH posts.userId TO users.id AS enriched
```

## 10. Iterate with bounded concurrency

```text
GET https://api.example.test/users AS users
FOR EACH user PARALLEL 8
    SAY "User: {user.name}"
```

Current `FOR EACH` limits are intentional:

- default maximum concurrency is 4;
- explicit `PARALLEL n` accepts 1..256;
- the body currently compiles **SAY actions only**.

HTTP enrichment or arbitrary mutation inside the loop is not part of the current surface contract.

## 11. Define a reusable TASK

```text
TASK fetch-user id -> Json
    GET https://api.example.test/users/{id} AS user

RUN fetch-user 42 AS user
SAY "{user.name}"
```

TASK expansion happens during compilation:

- parameters are substituted through explicit `{parameter}` placeholders;
- task-local aliases are hygienically renamed;
- `RUN ... AS result` attaches the public name to the final value-producing statement;
- recursive/cyclic expansion is rejected; the expansion depth is bounded.

## 12. Define a policy profile

```text
POLICY resilient
    RETRY 3
    TIMEOUT 30s
    CONTINUE ON ERROR

WITH resilient
    GET https://api.example.test/posts AS posts
    GET https://api.example.test/users AS users
```

Profiles are compile-time metadata that lower to the existing command execution policies. `BACKOFF` and status-specific `CONTINUE ON 404` are reserved/not implemented in the current policy contract.

## 13. Cache read results

```text
GET https://api.example.test/catalog CACHE 1h AS catalog
```

Current CACHE rules:

- duration suffixes: `ms`, `s`, `m`, `h`, `d`;
- only `Read` and `Pure` commands;
- cached commands currently need literal/resource inputs, not variable inputs;
- the default cache is in-memory and process-local.

## 14. Make a mutation idempotent

```text
POST order TO https://api.example.test/orders ONCE BY order.id
```

`ONCE BY` is valid only for effectful commands. The idempotency key combines the command fingerprint with the evaluated key expression. The default idempotency store is in-memory; a production host can replace it.

## 15. Read a secret safely

```text
GET secret:github-token AS token
```

This will not work in the default host because secret reads are deny-by-default. An embedding host must provide both an `ISecretStore` and an `ISecretAccessPolicy`.

A `Secret` is not `Text`; there is no implicit Secret-to-Text conversion. Do not expect `SAY [token]` to reveal a secret.

## 16. Compile an automation

Automation source uses a separate automation compiler API rather than `flunet run`:

```text
EVERY 1h
    GET https://api.example.test/status AS status
    SAY "Status: {status.name}"
```

Or a signal-driven definition:

```text
WATCH github.issues
    WHEN opened
        SAY "A new issue was opened"
```

The scheduler is host-driven: the host registers compiled definitions, calls `TickAsync(now)` for interval triggers and `PublishSignalAsync(...)` for watch triggers. FluNET does not create a hidden background scheduler thread.

## 17. Express desired state with ENSURE

```text
ENSURE backup.json CONTAINS https://api.example.test/config
REFRESH EVERY 1h
KEEP 7 VERSIONS
NOTIFY ON FAILURE
```

The ENSURE compiler turns the goal into the same GET/SAVE compilation pipeline. `REFRESH EVERY` can produce an automation definition. Runtime APIs also provide version retention for local file targets and failure notifications.

ENSURE is currently an experimental embedding API (`CompileEnsure` / `ExecuteEnsureAsync`), not a CLI subcommand.

## Next reading

- [Compact language reference](compact-language.md)
- [Automation and desired state](automation-and-desired-state.md)
- [Embedding and extensibility](embedding-and-extensibility.md)
- [Status and limitations](status-and-limitations.md)