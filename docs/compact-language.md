# Compact language reference

FluNET source is parsed into `SurfaceProgramSyntax`, transformed by compile-time passes, lowered to typed command frames, then compiled and executed by `SentenceExecutor`. There is one execution model for every source form.

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

For a more explicit spoken form, use `READ FILES`; each resource becomes its
own independent step and receives an inferred result name:

```text
READ FILES "post.json", "todo.json"
```

The same batch form is available with `LOAD FILES` and `GET FILES` when the
intent of the read should be explicit.

Structured files can be parsed without repeating the low-level output and
source markers:

```text
PARSE JSON "post.json" AS post
```

The compiler treats this as a typed `PARSE JSON [post] FROM {post.json}`
command. The bracketed form remains an internal frame representation; users
do not need to write it.

Several JSON files can be parsed in one sentence. Result names are inferred
from the file names and made unique when necessary:

```text
PARSE JSON FILES "post.json", "todo.json"
```

This produces two independent typed commands, equivalent to parsing `post`
and `todo` separately and joining them with `AND`.

A top-level `|` creates an explicit pipeline:

```text
GET https://api.example.test/posts AS posts | FILTER userId == 1 | TAKE 10
```

The same pipeline can be written one stage per line. `WHERE` is an alias for
`FILTER`, and `ORDER BY` is an alias for `SORT BY`:

```text
GET https://api.example.test/posts AS posts
WHERE userId == 1
ORDER BY title
SKIP 20
TAKE 10
```

Natural sentences may end with a period. A top-level period is equivalent to
a newline, while periods inside quotes and nested values remain data:

```text
GET users FROM https://api.example.test/users.
COUNT users AS total.
SAY "Loaded {total} users."
```

Text operations are small typed stages that compose through ordinary dataflow:

```text
GET https://api.example.test/message.txt AS message.
TRIM message AS clean.
REPLACE "warning" WITH "notice" IN clean AS friendly.
SPLIT friendly BY "," AS pieces.
COMBINE pieces WITH " | " AS oneLine.
```

`UPPER`, `LOWER` and `LINES` are also available. `COMBINE` is the text-list
operation; `JOIN` remains the structured-data join operation.

Assertions are executable stages, useful for API checks and smoke tests:

```text
GET https://api.example.test/health AS health.
EXPECT health.status TO EQUAL "ok".
EXPECT health.message TO CONTAIN "ready".
```

Supported comparisons are `EQUAL`, `CONTAIN`, `STARTS WITH`, `ENDS WITH` and
`MATCH`. A failed expectation stops the workflow with an actionable error.

## IMPORT

Larger programs can be split into local modules. Paths are resolved relative
to the importing file, imports are expanded before task and policy analysis,
and cycles are rejected:

```text
IMPORT "lib/file-tools.flu"
IMPORT "lib/health.flu"
RUN health-check AS report
```

Only local `.flu` and `.flunet` files are supported. This keeps module loading
portable and avoids hidden network or package-manager behavior.

Bounded repetition keeps operational scripts compact:

```text
REPEAT 3 TIMES:
    SAY "health check"
```

The block is expanded into ordinary ordered sentences before planning and
accepts counts from 0 through 10000.

Runtime loops keep polling and stateful scripts readable while remaining safe:

```text
LET attempts = 0
WHILE [attempts] < 3 MAX 10:
    INCREMENT [attempts]
EXPECT "{attempts}" TO EQUAL "3"
```

`WHILE` evaluates its condition before every iteration, observes cancellation,
and requires a bounded `MAX` limit (default 1000). Loop bodies can use `SAY`,
resource actions, `SET` and `INCREMENT`; assignments update the surrounding
workflow scope.

Loop control can stop or skip an iteration using a natural condition:

```text
WHILE attempts < 10 MAX 20:
    INCREMENT [attempts]
    BREAK WHEN attempts == 3
```

`CONTINUE WHEN condition` is also available. These controls are scoped to
`WHILE`; `FOR EACH` remains independently schedulable and cancellation-safe.

Conditional blocks use the same conversational form and accept unbracketed
variable names:

```text
IF ready:
    SAY "The service is ready."
ELSE IF starting:
    SAY "The service is starting."
ELSE:
    SAY "The service is still starting."
```

The compiler lowers both branches to ordinary commands with mutually exclusive
conditions, so they retain the normal plan, dependency and observability model.
`UNLESS condition:` is a negated `IF` form for guard-style scripts.

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
| CSV | built-in local decoder to `List<Json>` |
| XML | built-in local decoder to `Json` |
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

The built-in compact HTTP provider selects a typed decoder from the resource
format. Local CSV and XML files use the built-in structured decoders.

### Paginated JSON APIs

`PAGINATE` follows provider-returned `next` links and flattens the selected
array from each JSON page. Paths use dot notation, relative links are resolved
against the current URL, and `LIMIT` prevents an unbounded workflow:

```text
PAGINATE "https://api.example.test/items"
  ITEMS "data.items"
  NEXT "links.next"
  LIMIT 20
AS items
```

Requests still go through `IHttpTransport` (or the authenticated transport
when `USING` is present), so host network policy and credentials are applied
consistently with ordinary `GET`.

### Inspecting HTTP responses

Use `REQUEST` when a workflow needs status, headers and body together. Unlike
`GET`, it returns an envelope and does not discard a non-success status:

```text
REQUEST https://api.example.test/health AS response.
EXPECT response.status TO EQUAL "200".
EXPECT response.body.status TO EQUAL "ok".
```

The envelope contains `status`, `ok`, `headers` and a JSON-or-text `body`.
Network access and credentials still pass through the host-owned transport and
execution policy.

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

## SCAN files

`SCAN` is a portable source command for turning a file pattern into JSON file
metadata. It is backed by the `filesystem.scan` capability and respects the
host execution policy:

```text
SCAN "./data/*.json" AS files
FILTER extension == ".json" FROM files AS jsonFiles
SAY "Found {jsonFiles.length} files"
```

The command returns `path`, `name`, `nameWithoutExtension`, `extension`,
`directory`, `relativePath`, `length`, `createdUtc`, `modifiedUtc`,
`accessedUtc`, `isHidden` and `isReadOnly`. `LIST` exposes the same portable
metadata plus `isDirectory`. The
language does not depend on Windows shell commands; a host
may replace the provider with a sandboxed or platform-specific implementation.

`FIND` is the recursive form:

```text
FIND "./data/*.json" AS files
```

For a reusable metadata pass, use:

```text
INDEX FILES [files] FROM {./data} RECURSIVE
```

The result uses stable fields (`path`, `name`, `extension`, `length`, timestamps,
hidden and read-only flags). The host can replace the index provider without
changing the program.

Use `READ INDEX [files] FROM {./data}` to read the last snapshot. Indexed
queries support `WHERE`, `ORDER BY`, `TAKE` and `SKIP`; SQLite executes these
clauses in the database with parameterized, allow-listed fields. For example:

```text
READ INDEX [files] FROM {./data} WHERE {extension == '.json'} ORDER BY {modifiedUtc DESC} TAKE {50}
```

The CLI can persist the snapshot across processes with `--index ./flunet-index.db`,
or target a remote provider with `--index https://host/index/`.

Hosts can keep the snapshot current with the provider-neutral
`FileMetadataIndexWatcher`; it performs an initial rebuild and rebuilds after
portable create/change/delete/rename events, while the host controls lifetime
and cancellation.
Physical providers apply ordinary file changes incrementally; providers that do
not implement delta updates transparently fall back to a rebuild.

`SEARCH` scans file contents without invoking a platform shell. Plain text is
case-insensitive; `REGEX` enables a case-insensitive .NET regular expression:

```text
SEARCH "timeout" IN "./logs" RECURSIVE AS matches
SEARCH REGEX "error [0-9]+" IN "./logs" RECURSIVE AS errors
SEARCH "timeout" IN "./logs" RECURSIVE LIMIT 100 AS firstMatches
```

Each match contains `path`, `line`, `column` and the complete matching line.
Files whose first 4 KiB contain a NUL byte are treated as binary and skipped.

Because file results carry metadata, the normal data pipeline can filter them
immediately:

```text
FIND "./incoming" AS files
WHERE extension == ".json"
```

For a compact one-sentence query, attach the predicate directly to `FIND`:

```text
FIND "./incoming" WHERE extension == '.json' AS jsonFiles
```

Predicates also support readable text operators and simple file globs. They are
case-insensitive, which makes them useful for portable file tooling:

```text
FIND "./incoming" WHERE name CONTAINS 'report' AND name MATCHES '*.json' AS reports
FIND "./logs" WHERE extension STARTS WITH '.' AND name ENDS WITH '.log' AS logs
FIND "./archive" LIMIT 100 AS firstFiles
FIND "./archive" ORDER BY modified DESC SKIP 10 TAKE 20 AS page
```

`MATCHES` uses `*` for any sequence and `?` for one character. These operators
are compiled by the same `FILTERJSON` stage as `==`, comparisons and `AND`;
they are syntax sugar, not a second query engine.

`FIND`/`SCAN` also accept `LIMIT` (1–1,000,000). The physical filesystem
provider enforces the bound during enumeration, before metadata materialization.
`FIND` additionally accepts the same `ORDER BY`, `TAKE` and `SKIP` clauses as
`LIST`, all lowered into the shared typed collection stages.

File metadata also has short portable aliases: `size`/`bytes` means `length`,
`modified`/`created`/`accessed` address the corresponding UTC timestamps, and
`hidden`/`readonly` address visibility flags. Timestamp predicates can be read
naturally with `AFTER` and `BEFORE`:

```text
FIND "./incoming" WHERE size > 1048576 AND modified AFTER '2025-01-01T00:00:00Z' AS recentLarge
```

Size comparisons also accept readable decimal and binary units:
`10KB`, `10MB`, `10GB`, `1KiB`, `1MiB` and `1GiB`. The units are converted
before the provider-independent predicate runs, so the same script behaves
the same way on Windows, Linux and macOS.

ZIP, TAR and TAR.GZ archives expose the same query pipeline. The format is selected
from the archive extension (`.zip`, `.tar` or `.tar.gz`). List entries without extracting
them first:

```text
LIST ARCHIVE "./bundle.zip"
  WHERE isDirectory == false
  ORDER BY length DESC
  AS files
```

Each entry contains `path`, `length`, `compressedLength`, `modifiedUtc` and
`isDirectory`.

The same `PACK`/`UNPACK` syntax selects TAR automatically:

```text
PACK "./data" TO "./data.tar" AS archive
UNPACK "./data.tar" TO "./restored" AS files
PACK "./data" TO "./data.tar.gz" AS compressed
```

Structured rows can also be exported as CSV. Headers are inferred from the
union of object fields and values containing commas, quotes or newlines are
escaped according to RFC 4180:

```text
GET "https://api.example.test/people.csv" AS people
SAVE CSV [people] TO "./people.csv"
```

`LIST` enumerates both files and directories in one level, including portable
metadata useful for indexers, backup tools and automation:

```text
LIST "./data" AS entries
```

`LIST` can use the same compact query stages for directory indexes:

```text
LIST "./data" WHERE isDirectory == false ORDER BY modifiedUtc DESC TAKE 20 AS recent
```

The surface lowers this into `LISTFILES`, `FILTERJSON`, `SORTJSON` and
`TAKEJSON`; `RECURSIVE` can be placed after the path when a deep listing is
needed.

For one path, use `STAT`; missing paths return `exists: false` instead of
raising an exception:

```text
STAT "./data/report.json" AS info
```

File hashes are read-only and use SHA-256 by default:

```text
HASH "./data.json" AS digest
SAY "SHA-256: {digest}"
```

Portable runtime information is available without invoking an operating-system
shell:

```text
SYSTEM INFO AS system
SAY "Running {system.operatingSystem} on {system.architecture}"
```

Process metrics are available through the same cross-platform surface:

```text
METRICS AS metrics
SAY "PID {metrics.processId}, memory {metrics.workingSetBytes} bytes"
```

The result includes process id, working/private/managed memory, thread count,
processor time and host tick uptime. Values are observational and require no
shell or platform-specific command.

The same result includes portable `tempDirectory` and `homeDirectory` paths
for workflows that need staging or user-scoped data without invoking a shell.

Time and waiting are host capabilities as well:

```text
NOW AS started
WAIT 250ms
SAY "started at {started}"
```

`WAIT` accepts `ms`, `s`, `m`, `h` and `d`, is cancellation-aware and capped
at one day by the built-in provider. Hosts can inject a deterministic clock or
delay implementation for tests.

Host-owned notifications use a portable fallback and can be replaced by a
native provider through DI:

```text
NOTIFY "Backup completed"
```

Desktop clipboard access is also provider-owned and can be used without
platform-specific shell commands:

```text
READ CLIPBOARD AS source
COPY "Backup completed" TO CLIPBOARD
```

The default host exposes the capability only when a native clipboard is
available; embedding hosts can replace it with their own provider.

Provider-neutral messaging uses the same short form:

```text
PUBLISH "Backup completed" TO "jobs.backup"
```

Workflows can synchronously consume the next message:

```text
RECEIVE "jobs.backup" AS job
```

The default host uses an in-memory queue. The canonical CLI can persist it with
`--queue .flunet/messages.jsonl`; embedding hosts can inject the
single-host durable `JsonFileFluNetMessageBus` or a remote
`IFluNetMessageBus` implementation.

Portable file mutations are explicit and return the destination file:

```text
COPY "./report.json" TO "./backup/report.json" AS backup
MOVE "./incoming/report.json" TO "./processed/report.json" AS report
```

Directory listing can include all descendants without a platform-specific
recursive flag:

```text
LIST "./incoming" RECURSIVE AS entries
```

`TRASH` is recoverable: the portable provider moves the file to a local
`.flunet-trash` directory instead of permanently deleting it.

```text
TRASH "./old-report.json" AS removed
```

Small application state can use the portable key-value capability:

```text
STORE "theme" = "dark" AS stored
READ "theme" AS theme
SAY "Theme: {theme}"
```

The default host keeps these values in memory. Embedding hosts can register
`JsonFileFluNetKeyValueStore` for atomic JSON state, or `SqliteFluNetKeyValueStore` for a durable SQLite database.

The canonical CLI can opt into that durable backend with `--store PATH`:

```text
flunet --store .flunet/values.json -- "STORE \"theme\" = \"dark\" AS saved"
flunet --store .flunet/values.json -- "READ \"theme\" AS theme"
flunet --store .flunet/values.db -- "LIST STORE \"user:\" AS users"
```

The file remains subject to the active `--root` execution policy.

The packaged tool accepts the same durable backends on compact runs:

```text
flunet run workflow.fln --store .flunet/values.json --queue .flunet/messages.jsonl --blob .flunet/blobs
```

Provider-neutral text blobs use the `blob:` resource scheme:

```text
SAVE report TO blob:reports/latest
GET blob:reports/latest AS latest
DELETE blob:reports/latest AS removed
```

The default host keeps blobs in memory. Hosts can replace
`IFluNetBlobStore` with a local directory or a cloud/object-storage adapter.
The CLI enables the local durable provider with `--blob .flunet/blobs`.

The built-in `HttpFluNetBlobStore` is a provider-neutral object gateway
adapter. It keeps the same `blob:` syntax, validates relative keys and checks
each request through the active network policy.

Hosts may also provide `S3FluNetBlobStore` for AWS Signature V4-compatible
object storage. It supports AWS S3, MinIO and compatible gateways through the
same blob commands; credentials and endpoint selection remain host-owned.

Capability discovery is available inside workflows as well as through the CLI:

```text
CAPABILITIES [caps]
```

Each row contains the capability id, version, current-platform availability,
supported platforms and required permissions. This lets a program choose a
provider fallback without branching on Windows, Linux or macOS names.

Host-installed provider packages can be inspected with:

```text
PACKAGES [packages]
```

Package manifests are declarative: they expose an id, version, entry point,
supported platforms, capabilities and permissions. The catalog does not load
arbitrary assemblies; the host remains responsible for approving and wiring
the entry point.

For a safe host smoke test, use:

```text
DOCTOR [report]
```

The report includes runtime/platform, capability counts, package count and
active provider type names; it deliberately excludes secret values.

## SQLite

The SQL surface has a provider-neutral query boundary. Hosts that want local
portable persistence can opt into the built-in SQLite adapter:

```csharp
services.AddSingleton<ISqlQueryExecutor>(provider =>
    new SqliteFluNetQueryExecutor(
        "./data/app.db",
        provider.GetRequiredService<IExecutionPolicy>()));
```

The compact query remains small and provider-independent:

```text
GET sql:"SELECT id, title FROM posts ORDER BY id" AS posts
SAY "Loaded {posts}"
```

Use `$name` placeholders for values coming from FluNET variables. The runtime
binds them as database parameters, so user data never needs to be concatenated
into SQL:

```text
GET sql:"SELECT id, title FROM posts WHERE author = $author" AS posts
```

The same parameter contract is used by SQLite and the generic ADO.NET adapters.
Hosts that reference another provider package can use
`DbProviderFactorySqlQueryExecutor` with its `DbProviderFactory` and connection
string; the FluNET engine does not need a provider-specific dependency.
Missing variables fail before the query is sent to the database.

Mutations are explicit and return the affected row count:

```text
APPLY SQL "UPDATE posts SET published = 1 WHERE id = $postId" AS changed
```

`APPLY SQL` uses the same `$name` parameter binding and remains disabled when
the host exposes only the default denied SQL capability.

The automation tool accepts `.jsonl` by default and selects SQLite for an
events path ending in `.db` or `.sqlite`. The same extension convention is
used by the CLI `--queue PATH` option for durable `PUBLISH`/`RECEIVE`:

```text
flunet automation watch workflow.flunet ./inbox files --events ./events.db
flunet automation replay workflow.flunet ./events.db
```

Blob keys can be indexed without knowing their exact names:

```text
LIST BLOB "reports/" AS keys
WHERE key MATCHES '*.json'
ORDER BY key
```

The in-memory, file and HTTP object providers share the same prefix contract.

The key-value store has the same compact index operation and includes values:

```text
LIST STORE "user:" AS values
WHERE key MATCHES 'user:*'
```

Keys can be removed explicitly, without affecting local files:

```text
DELETE STORE "user:temporary" AS removed
```
Hosts can optionally pass a `SecretValue` and authentication scheme to the HTTP
provider; the same credential is applied to object reads, writes, deletes and
prefix listing without exposing it to the language surface.

SQL is denied by the default host. When configured, the capability graph shows
`surface.get.sql -> database.sql -> SqliteFluNetQueryExecutor`, and the file is
still checked by the host execution policy.

## Processes

`EXECUTE` starts one executable directly and captures its result. It never
passes the command through CMD, PowerShell, Bash or another shell:

```text
EXECUTE "dotnet --version" AS runtime
SAY "exit={runtime.exitCode}, output={runtime.standardOutput}"

# Set a portable working directory; execution remains direct and shell-free.
EXECUTE "dotnet --info" IN "./tools" AS runtime

# Pass only the explicitly listed environment overrides to the child process.
EXECUTE "dotnet test" IN "./src" ENV {DOTNET_NOLOGO=1, MODE="ci"} AS test
```

Process execution is denied by the default host. An embedding application must
register `PhysicalFluNetProcessRunner` (and should add its own executable
allow-list) as `IFluNetProcessRunner`. Arguments and the optional `IN` working
directory and explicit `ENV` overrides are passed through portable .NET process
APIs; paths containing spaces do not require platform-specific quoting. The
child inherits the host environment unless the host replaces the process
runner with a stricter provider.

For long-lived direct processes, hosts that opt into process execution can use
the session trio. `START` returns an opaque session id, which can be sent to
later commands:

```text
START "dotnet watch" AS session
SEND "status" TO [session] AS response
STOP [session] AS result
```

Sessions are registry-owned and are denied by the default host. They never
invoke CMD, PowerShell, Bash or another operating-system shell.

## Portable system paths

Resolve host-specific directories without embedding operating-system rules in a
program:

```text
PATH TEMP AS temporary
PATH CACHE AS cache
PATH HOME AS home
PATH CURRENT AS working
```

Supported names include `CURRENT`, `TEMP`, `HOME`, `DESKTOP`, `DOCUMENTS`,
`DOWNLOADS`, `APPDATA`, `LOCALAPPDATA`, `CACHE`, and `PROGRAMDATA`. A host may
replace `IFluNetPathResolver` when its filesystem policy needs a different
mapping.

Environment reads remain compact with `GET env:NAME`. Environment mutation is
explicit and host-controlled:

```text
SET ENV MODE TO "test" AS changed
```

The default host denies this operation. The CLI requires
`--allow-environment-write`, and embedders must install an `IEnvironmentWriter`
and its policy. This changes the current process environment only; it does not
rewrite shell profiles or operating-system configuration.

Host-owned configuration is available through the parallel `config:` resource:

```text
GET config:api.base AS apiBase
GET config:feature.experimental AS enabled
```

Canonical syntax can address the same provider explicitly:

```text
GETCONFIG [apiBase] FROM {api.base}
```

Hosts provide `IFluNetConfiguration` from appsettings, container config, a
vault adapter, or a test dictionary. Missing keys fail explicitly.

Create host-owned scratch artifacts without knowing the platform temp folder:

```text
TEMP FILE AS artifact
TEMP FILE .json AS payloadFile
TEMP DIRECTORY AS workspace
CLEANUP [artifact]
CLEANUP [workspace]
```

The `system.temp` provider applies the same filesystem policy as other local
operations. `CLEANUP` only accepts paths created by the same `system.temp`
provider, so it cannot be used as an unrestricted delete operation.

Directory transfers use the same compact form as file transfers by adding the
`DIRECTORY` qualifier:

```text
COPY DIRECTORY "./reports" TO "./reports-backup" AS backup
MOVE DIRECTORY "./staging" TO "./archive" AS archived
TRASH DIRECTORY "./old-report" AS removed
RESTORE [removed] TO "./old-report" AS restored
```

Copy is recursive and both operations are checked by the filesystem policy.
`TRASH DIRECTORY` moves the complete tree into the adjacent `.flunet-trash`
folder, preserving the recoverable-trash behavior of file `TRASH`.
`RESTORE` and `RESTORE DIRECTORY` accept only items directly inside
`.flunet-trash`, preventing arbitrary paths from being treated as trash.

## Archives

ZIP packaging and extraction are portable and do not require an installed
archive utility:

```text
PACK "./report" TO "./report.zip" AS bundle
UNPACK "./report.zip" TO "./restored" AS files
```

Both operations are checked by the file execution policy and lower to the
`filesystem.archive` capability.

Create output directories without platform-specific shell syntax:

```text
MKDIR "./reports/daily" AS directory
SAVE report TO "./reports/daily/report.json"
```

`MKDIR` is idempotent and lowers to `filesystem.directory`.

## Reliability directives

Within a lexical context you can set retry/timeout defaults:

```text
FROM https://api.example.test
    RETRY 3
    TIMEOUT 10s
    GET posts AS posts
```

These directives are lowered to the normal canonical execution policy. Supported timeout suffixes in the execution planner include milliseconds/seconds/minutes/hours.

`AUTH secret:name` applies the selected secret to the following HTTP resource or
mutation in the current block. The secret remains opaque and is revealed only
inside the authenticated transport:

```text
AUTH secret:github-token
PUT payload TO https://api.example.test/items/1
```

## SAVE

```text
SAVE value TO output.txt
SAVE post TO post.json
```

Simple identifiers are treated as variable references. The target is a file path/reference consumed by the canonical SAVE frame. When the current
pipeline value is used, the sink can omit the value:

```text
GET https://api.example.test/posts AS posts
SAVE TO report.json
```

`SAVE TO *.json` selects the JSON encoder automatically. Explicit
`SAVE value TO ...` remains text-compatible for existing programs.

## POST

```text
POST order TO https://api.example.test/orders
```

Compact POST currently targets an absolute HTTP(S) URI and lowers to the canonical JSON POST frame.

The same provider-neutral surface supports the remaining common API mutations:

```text
PUT payload TO https://api.example.test/items/1
PATCH changes TO https://api.example.test/items/1
DELETE https://api.example.test/items/1 AS removed
```

All three use the active network policy and are ordered external mutations.

## EMIT

Send a JSON/text event to a host-owned sink. The default sink is an HTTP
webhook; hosts can replace `IFluNetEventSink` with a broker, queue, or native
integration:

```text
EMIT order TO https://hooks.example.test/orders
EMIT [event] TO https://hooks.example.test/events USING {webhook-secret}
```

The event payload and endpoint are evaluated by the normal variable binder,
and delivery remains an ordered external mutation.

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

Explicit block connectors are also accepted in compact source:

```text
GET first.json AS first
THEN
GET second.json AS second
AND
SAY "both reads are complete"
```

`THEN`/`SEQUENCE` create an ordering barrier; `AND`/`PARALLEL` preserve
parallel coordination; `ELSE` preserves the canonical alternative edge.
The barrier also attaches to the first lowered command of a multi-stage
pipeline, so `THEN` remains reliable before `FIND`, `LIST ... ORDER BY ...`
and `GET ... | FILTER ...` forms.
`WITH RETRY {3} WITH TIMEOUT {30s}` and `ON ERROR CONTINUE` can be placed on
their own line and apply to following commands in the current block.

## Pipelines

Aggregates can consume a named collection directly, which keeps independent
data flows readable without artificial ordering steps:

```text
GET https://api.example.test/users AS users
GET https://api.example.test/posts AS posts
COUNT [users] AS userCount
COUNT [posts] AS postCount
SAY "Loaded {userCount} users and {postCount} posts"
```

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

## DISTINCT and SKIP

```text
DISTINCT
DISTINCT BY userId
SKIP 20
```

`DISTINCT` preserves the first occurrence and accepts an optional row key.

## Natural-language sugar

Surface commands accept a small, deterministic vocabulary of natural aliases.
They lower to the same canonical commands and do not change the execution model:

```text
FETCH https://api.example.test/users AS users
KEEP THE FIRST 10
WHERE their.active == true
REMOVE DUPLICATES BY their.email
SAVE THEM AS active-users.json
```

The following aliases are supported: `FETCH`/`RETRIEVE` for `GET`,
`STORE`/`WRITE` for `SAVE`, `OUTPUT` for `SAY`, `KEEP THE FIRST n` for
`TAKE n`, and `REMOVE DUPLICATES [BY expression]` for `DISTINCT`.
After a data stage, `IT`, `THEM`, `THIS`, `THE RESULT`, `THE RESPONSE`,
`their.field` and `its.field` refer to the current pipeline value.

`OTHERWISE` is an alias for `ELSE`, and sorting accepts natural direction words:

```text
ORDER BY createdAt NEWEST
```

In the interactive session, `:explain PROMPT` displays the inferred lowering,
dependency graph and execution policies before running anything.

## Aggregates

Aggregates return a scalar number and can be used after a collection stage:

```text
COUNT
AVG score
MIN price
MAX price
```

`COUNT` counts rows. The other forms ignore null values and require numeric
expressions.

## LET and null safety

`LET` is compact sugar for a typed `SET` command:

```text
LET retries = 3
LET enabled = true
LET label = "daily report"
```

The conversational equivalent is also accepted:

```text
SET retries TO 3
SET [label] TO "daily report"
```

Both forms lower to the same typed assignment.

Expressions support null-safe property access and coalescing:

```text
SELECT user?.address?.city ?? "Unknown"
```

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

## Reusable tasks

`TASK`/`RUN` provide local module-like composition without a second runtime:

```text
TASK fetch-user id RETURNS Json
    GET https://api.example.test/users/{id} AS user
    RETURN [user]

RUN fetch-user 42 AS user
SAY user.name
```

Task calls are expanded and type-checked before normal lowering. Aliases are
namespaced per call, recursive expansion is rejected, and `RUN ... AS name`
binds the final value-producing stage to the requested name.

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
- body supports `SAY`, `NOTIFY`, `PUBLISH`, `GET`, `LOAD`, `SAVE`, `POST`, `MKDIR`, `COPY`, `MOVE`,
  `PACK`, `UNPACK` and `TRASH` actions;

Filesystem actions execute through the same capability providers as top-level
commands, while each iteration keeps its own variable scope.

## TASK and RUN

Definition:

```text
TASK fetch-user id RETURNS Json
    GET https://api.example.test/users/{id} AS user
    RETURN [user]
```

Call:

```text
RUN fetch-user 42 AS user
SAY "{user.name}"
```

Header shape:

```text
TASK name [parameter ...] [RETURNS Type]
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
- `RETURNS Type` declares the task result contract;
- `RETURN value` makes the returned expression explicit;
- non-`Unit` tasks must contain an explicit `RETURN`;
- `AS result` names the final value-producing statement;
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

For local files, hosts can connect `IFluNetFileWatcher` events to the existing
`WATCH`/`WHEN` signal bridge. The watcher is an async stream with explicit
cancellation; it does not create a hidden scheduler thread.

The built-in `FileWatchAutomationBridge` performs that connection while the
host still owns its lifetime:

```text
WATCH incoming.files
  WHEN CREATED
    SAY "A file arrived"
```

Hosts can pass `FluNetFileWatchOptions` to the bridge/watcher to select a glob,
recursive traversal and duplicate-event debounce. Each event carries the
change kind, path, optional old path, UTC timestamp, directory flag and known
file length.

The signal is also available inside the workflow as the dynamic root `event`:

```text
WATCH incoming.files
  WHEN CREATED
    SAY "{event.kind}: {event.path} ({event.length} bytes)"
```

Available fields include `event.resource`, `event.name`, `event.kind`,
`event.path`, `event.oldPath`, `event.timestamp`, `event.isDirectory`, and
`event.length`.

See [Automation and desired state](automation-and-desired-state.md).

## ENSURE syntax is separate

`ENSURE` is compiled by `EnsureCompiler` and executed through the embedding API.

See [Automation and desired state](automation-and-desired-state.md).

## Not a current compact feature

The current `main` does **not** expose a supported public compact implementation for:

- SQL mutation statements and database-specific transaction syntax;
- generic `AUTH` application;
- `SYNC`/general reconciliation;
- generic compensation language syntax;
- distributed workflow coordination.

See [Status and limitations](status-and-limitations.md) for the authoritative support matrix.
