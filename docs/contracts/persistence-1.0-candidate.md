# FluNET 1.0 persistence contract candidate

Persistence is intentionally split into independent contracts rather than one database abstraction:

- workflow journal (`IWorkflowStateStore`);
- automation next-due state (`IAutomationScheduleStore`);
- execution-result cache (`IExecutionResultCache`);
- idempotency records (`IIdempotencyStore`);
- ENSURE versions (`IEnsureVersionStore`);
- workflow run catalog (`IWorkflowRunCatalog`).

`PersistenceContractInspector` reports which implementation is configured and classifies it as process-local, built-in single-host durable, or external/custom.

A host may therefore make idempotency durable without also making cache durable, or use an external transactional workflow journal while keeping automation scheduling elsewhere. This separation is part of the 1.0 contract candidate.

Built-in durable stores are single-host stores; this contract does not claim distributed transactional semantics.
