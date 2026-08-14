# FluNET 0.9 plan — Declarative Reconciliation

**Status: IMPLEMENTED SOURCE CANDIDATE / NOT VERIFIED.**

0.9 turns the ENSURE foundation into a general desired-state/reconciliation layer without introducing a second command executor.

## Batch completion

- **66 — Desired/Observed State IR** — implemented;
- **67 — resource observation** — implemented;
- **68 — `SYNC` compilation** — implemented;
- **69 — Create/Update/Delete/Unchanged/Conflict diff IR** — implemented;
- **70 — reconciliation planning and execution** — implemented;
- **71 — WATCH-triggered reconciliation** — implemented;
- **72 — compensation contracts** — implemented;
- **73 — saga execution** — implemented;
- **74 — audit/history API** — implemented;
- **75 — freeze/stabilization documentation** — implemented, verification pending.

## Resulting architecture

```text
SYNC target WITH source BY id
        |
        v
   SyncCompiler
        |
        v
   SyncDefinition
        |
        v
IResourceObserverRegistry
   /             \
 target         source
   \             /
 Desired / Observed State
        |
        v
ReconciliationDiffEngine
        |
        v
ReconciliationMutationPlanner
        |
        v
 SurfaceCompiler
        |
        v
 ExecutionPlanExecutor
```

The compiler also keeps a side-effect-free two-read analysis graph on each `SyncDefinition`; runtime state acquisition itself uses the observer boundary. This prevents the old compact `LOAD CONFIG` shape from constraining reconciliation of JSON arrays.

See [0.9 freeze readiness](compiler-0.9-freeze-readiness.md) for the exact contract and verification gate.
