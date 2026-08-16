# FluNET 0.8 plan — Integration & Execution

0.8 closes the gap between the compact/data/automation language already present in the source tree and a coherent integration runtime.

Planned work is tracked as Batches 53–65 in `roadmap.md`:

1. resource payload/decoder/encoder registry;
2. CSV/XML support;
3. Binary/Image values;
4. generic HTTP media responses;
5. SQL provider;
6. AUTH profiles bound to opaque secrets;
7. compiled nested actions;
8. full FOR EACH bodies;
9. backoff/jitter/status-specific policies;
10. durable cache/idempotency;
11. calendar/cron triggers;
12. automation/ENSURE CLI;
13. tooling/freeze candidate.

Architectural invariant: all new front-end forms must lower/compile to the same typed command graph and ultimately execute through the canonical `Executor`.
