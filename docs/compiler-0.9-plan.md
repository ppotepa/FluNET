# FluNET 0.9 plan — Declarative Reconciliation

0.9 turns the ENSURE foundation into a general desired-state/reconciliation layer without introducing a second command executor.

Batches 66–75 cover:

- Desired/Observed State IR;
- resource observation;
- `SYNC` compilation;
- Create/Update/Delete/Unchanged/Conflict diff IR;
- reconciliation planning and execution;
- WATCH-triggered reconciliation;
- compensation contracts;
- saga execution;
- audit/history API;
- freeze candidate documentation.

Reconciliation produces ordinary execution plans. Providers may contribute observation/mutation/compensation capabilities, but orchestration remains in the shared planner/executor stack.
