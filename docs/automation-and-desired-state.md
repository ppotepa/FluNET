# Automation and desired state

FluNET has two advanced compilation APIs above ordinary compact programs:

- **automation**: `EVERY` / `WATCH` / `WHEN` compile trigger metadata plus an already-compiled ordinary workflow plan;
- **desired state**: `ENSURE` compiles a goal into an ordinary GET/SAVE plan and optional interval automation.

Neither layer introduces a second command executor. Workflow bodies still run through `SentenceExecutor`.

> These are experimental source-level APIs on `main`. They are not exposed as `flunet run` language constructs and the exact tree still requires Release build/test verification.

## Automation source

### EVERY

```text
EVERY 1h
    GET https://api.example.test/status AS status
    SAY "Status: {status.name}"
```

Supported interval suffixes include `ms`, `s`, `m`, `h` and `d`. Intervals must be positive and are capped by the compiler.

### WATCH

Direct body:

```text
WATCH catalog.changed
    GET https://api.example.test/catalog AS catalog
    SAY "Catalog was refreshed"
```

### WATCH + WHEN

```text
WATCH github.issues
    WHEN opened
        SAY "A new issue was opened"
```

`WHEN` is currently recognized as the first nested child of a WATCH block. It needs an event name and an indented workflow body.

Watch workflows receive the triggering signal through the dynamic `event`
root, for example `{event.path}` and `{event.length}`. File-watch adapters
populate the path, old path, kind, timestamp, directory flag and length fields.

Hosts can persist incoming signals through `IAutomationSignalStore`. The
bundled tool exposes the JSONL adapter for file watches:

```text
flunet automation watch workflow.flunet ./incoming incoming.files --events ./events.jsonl
```

The journal is append-only and provider-neutral; an embedder can replace it
with a database or queue implementation without changing the language.

Stored signals can be replayed through the same automation plans:

```text
flunet automation replay workflow.flunet ./events.jsonl --event CREATED
```

Replay preserves journal order and can restrict delivery to one event kind.

Scheduled automations can also be kept alive by the CLI. The daemon uses the
same `AutomationScheduler` and durable schedule state, and stops cleanly on
Ctrl+C:

```text
flunet automation daemon workflow.flu --state .flunet/schedule.json --interval 1s
```

## Compile automation definitions

```csharp
using FluNET.Automation;
using FluNET.Context;

using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();

AutomationCompilationResult compiled = context.CompileAutomations("""
EVERY 1h
    GET https://api.example.test/status AS status
    SAY "Status: {status.name}"
""");

if (!compiled.IsValid)
{
    foreach (AutomationDiagnostic diagnostic in compiled.Diagnostics)
        Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
}
```

Each `AutomationDefinition` contains:

- stable generated automation id;
- `TriggerDefinition` (`IntervalTriggerDefinition` or `WatchTriggerDefinition`);
- `WorkflowTemplate` containing a normal `SurfaceCompilationResult` and `ExecutionPlan`;
- source span.

## Host-driven scheduler

The current `AutomationScheduler` deliberately owns **no background thread or timer**. The embedding host decides when to poll the clock or deliver an external signal.

```csharp
using FluNET.Automation;
using FluNET.Execution.Planning;

AutomationScheduler scheduler = new(
    context.GetService<SentenceExecutor>(),
    new InMemoryAutomationScheduleStore());

DateTimeOffset now = DateTimeOffset.UtcNow;
foreach (AutomationDefinition definition in compiled.Automations)
    await scheduler.RegisterAsync(definition, now);

// Host calls this from its own timer/service loop.
IReadOnlyList<AutomationRunResult> due =
    await scheduler.TickAsync(DateTimeOffset.UtcNow);

// Host calls this when an external source produces an event.
IReadOnlyList<AutomationRunResult> signaled =
    await scheduler.PublishSignalAsync("github.issues", "opened");
```

For local files, the packaged tool supplies a portable watcher bridge:

```text
flunet automation watch automation.fln ./incoming files.changed --recursive
```

It translates `Created`, `Changed`, `Deleted` and `Renamed` filesystem events
into `WATCH`/`WHEN` signals and stops cleanly on Ctrl+C.

The scheduler executes the precompiled plan through the canonical executor and returns per-run results.

## Durable schedule state

`InMemoryAutomationScheduleStore` loses next-due timestamps on process restart. Use `DurableAutomationScheduleStore` when the host needs timer state to survive restarts:

```csharp
using FluNET.Automation;
using FluNET.Capabilities;

IExecutionPolicy policy = context.GetService<IExecutionPolicy>();
IAutomationScheduleStore store = new DurableAutomationScheduleStore(
    ".flunet/automation-schedule.json",
    policy);

AutomationScheduler scheduler = new(
    context.GetService<SentenceExecutor>(),
    store);
```

The durable schedule store:

- checks file access through `IExecutionPolicy`;
- writes through a temporary file;
- uses write-through I/O and flush-to-disk;
- persists schedule state, **not compiled definitions**.

After restart the host must recompile/re-register its automation definitions; existing `NextDue` state is then reused.

## ENSURE

ENSURE describes a desired file state instead of spelling out GET/SAVE manually.

Minimal goal:

```text
ENSURE backup.json CONTAINS https://api.example.test/config
```

Compiler lowering conceptually produces:

```text
GET https://api.example.test/config AS __ensure_0000
SAVE __ensure_0000 TO backup.json
```

but it creates AST/IR directly rather than generating and reparsing command strings.

### Refresh

```text
ENSURE backup.json CONTAINS https://api.example.test/config
REFRESH EVERY 1h
```

A valid refresh interval produces an `AutomationDefinition` whose workflow template is the same compiled desired-state plan.

### Version retention

```text
ENSURE backup.json CONTAINS https://api.example.test/config
KEEP 7 VERSIONS
```

For local file targets the runner can capture the previous content after a successful change.

Default version store:

```csharp
IEnsureVersionStore versions = new InMemoryEnsureVersionStore();
```

Durable directory store:

```csharp
using FluNET.Declarative;
using Microsoft.Extensions.DependencyInjection;

using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(services =>
{
    services.AddDirectoryEnsureVersions(".flunet/ensure-versions");
});
```

`DirectoryEnsureVersionStore` keeps only the configured number of `.bak` files per target and validates access through the host execution policy.

### Failure notification

```text
ENSURE backup.json CONTAINS https://api.example.test/config
NOTIFY ON FAILURE
```

`IDesiredStateNotifier` is the extension point. The default fallback used by `EnsureExecutionExtensions` writes a failure message through `ITextOutput`.

### Combined example

```text
ENSURE backup.json CONTAINS https://api.example.test/config
REFRESH EVERY 1h
KEEP 7 VERSIONS
NOTIFY ON FAILURE
```

Current option limits:

- `REFRESH EVERY`: `s`, `m`, `h`, `d`, positive and capped at 365 days;
- `KEEP`: 1..10000 versions;
- unknown ENSURE options produce diagnostics.

## Compile ENSURE without running

```csharp
using FluNET.Context;
using FluNET.Declarative;

DesiredStateCompilationResult result = context.CompileEnsure("""
ENSURE backup.json CONTAINS https://api.example.test/config
KEEP 7 VERSIONS
""");
```

## Execute ENSURE

The source API also exposes:

```csharp
IReadOnlyList<EnsureRunResult> results = await context.ExecuteEnsureAsync("""
ENSURE backup.json CONTAINS https://api.example.test/config
KEEP 7 VERSIONS
NOTIFY ON FAILURE
""");
```

Treat the advanced desired-state runtime as experimental until the repository's Release verification gate is green for the exact tree.

## Workflow durability vs automation durability

These are separate responsibilities:

- `IWorkflowStateStore` journals command-step execution and enables workflow resume;
- `IAutomationScheduleStore` persists the next due time for interval automations;
- automation definitions are source/compiler artifacts and must be re-registered by the host after restart.

See [Durable workflows](durable-workflows.md) for workflow journal configuration.

## Current non-features

The current `main` should not be documented as providing a released/general solution for:

- cron expressions;
- an always-running internal scheduler service;
- arbitrary event-source adapters (GitHub/Slack/etc. must be supplied by the host and call `PublishSignalAsync`);
- generic SYNC/reconciliation language;
- distributed scheduler coordination.

See [Status and limitations](status-and-limitations.md).
