# Durable workflows

FluNET's execution planner/executor has one workflow journal protocol, `IWorkflowStateStore`. Durability is therefore a storage choice, not a second executor or resume mechanism.

## What is persisted

The executor records `WorkflowEvent` entries containing information such as:

```text
RunId
StepIndex
Status
Attempt
Timestamp
Message
ResultJson
PlanFingerprint
```

Terminal step results can be restored when a workflow is resumed.

## Resume semantics

A host supplies a stable run id:

```csharp
using FluNET.Execution.Workflow;

Guid runId = Guid.Parse(
    "1d98aa21-e92e-41da-9807-98fe5153ad61");

ExecutionResult first = await engine.ExecuteAsync(
    new ProcessedPrompt(
        "GET [text] FROM {input.txt} THEN SAY [text]."),
    new WorkflowExecutionOptions(runId));
```

Resume the same execution plan:

```csharp
ExecutionResult resumed = await engine.ExecuteAsync(
    new ProcessedPrompt(
        "GET [text] FROM {input.txt} THEN SAY [text]."),
    new WorkflowExecutionOptions(
        runId,
        Resume: true));
```

The executor compares the journal's plan fingerprint with the current plan. Reusing a run id with changed commands is rejected rather than silently mixing histories.

## Default in-memory store

`FluNETContext` registers:

```text
IWorkflowStateStore = InMemoryWorkflowStateStore
```

This is suitable for process-local execution/tests. It cannot resume after process restart.

## Simple JSON-lines store

`JsonFileWorkflowStateStore` is the older/simple single-host file implementation. It writes one JSON object per line and serializes access per run inside the process.

Use it when simple restart persistence is sufficient and your host owns the lifecycle/configuration.

## Checksummed durable store

`DurableWorkflowStateStore` is the stronger single-host file implementation.

Configure it through DI:

```csharp
using FluNET.Context;
using FluNET.Execution.Workflow;

using FluNETContext context =
    SurfaceCompilationExtensions.CreateSurfaceContext(
        services =>
        {
            services.AddDurableFluNetWorkflows(
                ".flunet/workflows");
        });
```

The host `IExecutionPolicy` must allow the journal directory.

### Storage properties

Each run is stored as an append-only checksummed JSON-lines journal.

For every event the store writes an envelope containing:

```text
EventJson
SHA-256 checksum(EventJson)
```

Durability behavior includes:

- per-run in-process serialization;
- append-only records;
- `FileOptions.WriteThrough`;
- asynchronous flush followed by `Flush(flushToDisk: true)`;
- checksum validation while reading;
- rejection of invalid/truncated/corrupt non-empty records;
- run-id validation against the journal filename.

The implementation deliberately fails on corruption instead of skipping a broken line and creating a misleading partial history.

## Workflow result serialization

`IWorkflowValueSerializer` controls how successful command results are stored/restored.

The default `JsonWorkflowValueSerializer` handles ordinary JSON-serializable values and special built-in boundaries such as:

```text
FileInfo
DirectoryInfo
Uri
```

Extensions with custom result types can replace the serializer.

## Retry and durable events

Retries do not bypass the journal. Running/retrying/succeeded/failed/skipped transitions are recorded through the same workflow event model. On resume, the executor reconstructs terminal steps and leaves unfinished work pending.

## Surface programs

Compact programs also produce an ordinary `ExecutionPlan`, so the same workflow store is used when `ExecuteSurfaceAsync` reaches the executor. There is no compact-specific durability protocol.

## Automation schedule durability is separate

Automation interval state answers a different question: *when should a compiled automation fire next?*

Use `IAutomationScheduleStore` / `DurableAutomationScheduleStore` for `NextDue` timestamps. That store does not replace `IWorkflowStateStore`.

A durable automation host may therefore configure both:

```text
DurableAutomationScheduleStore
    -> next due time

DurableWorkflowStateStore
    -> execution-step history/results
```

Automation definitions themselves are compiler artifacts and must be recompiled/re-registered after restart.

## ENSURE and durability

ENSURE plans eventually execute ordinary GET/SAVE plans, so command-level workflow journaling uses the same executor/store. ENSURE version retention is a separate desired-state concern handled by `IEnsureVersionStore`.

For local ENSURE targets the optional `DirectoryEnsureVersionStore` can persist historical target contents independently of the workflow journal.

## Single-host boundary

The current durable workflow stores target **single-host** persistence. They do not provide:

- distributed leases;
- consensus;
- transactional cross-node claims;
- exactly-once mutation semantics across multiple machines;
- a distributed scheduler.

If multiple processes/nodes must coordinate workflow ownership, provide an application-specific store/coordination layer with appropriate transactional semantics. Do not treat the file journal as a distributed lock.

## Capability boundary

Durable file stores call `IExecutionPolicy.EnsureFileAccess(...)` before touching their configured paths. Production hosts should keep journal/schedule/version directories inside explicitly allowed roots.

## Verification note

The source-level durable APIs are present on `main`, but the exact repository tree still requires the documented Release restore/build/test gate before a release claim.