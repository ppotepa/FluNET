# Durable workflows

`DurableWorkflowStateStore` is the single-host durable implementation of the existing `IWorkflowStateStore` contract. It does not introduce a second resume protocol: `ExecutionPlanExecutor` reads the same `WorkflowEvent` history used by the in-memory store.

Each run is an append-only checksummed JSON-lines journal. Appends use write-through file I/O and an explicit disk flush. A corrupt or truncated non-empty record is rejected instead of being silently ignored. The store deliberately permits one writer per run; distributed leasing/coordination is a later layer.

Configure it with `services.AddDurableFluNetWorkflows(directory)`. The host execution policy must allow the journal directory.
