# Engineering quality gates

`coverage-baseline.txt` is the committed minimum Cobertura line-rate for the release gate.
The current floor is 0.40 (40%). Lowering it requires an explicit code-review decision; normal
coverage updates should only keep or raise the value. The verification scripts fail when the
reported line-rate for `FluNET.Engine` plus the `flunet` tool assembly falls below this floor.

The baseline is intentionally conservative until the repository has a runnable GitHub Actions
worker that can publish a fresh measured report. Once a complete release-gate run is available,
raise the file to the observed stable line-rate (rounded down slightly to avoid platform noise)
and treat subsequent decreases as regressions.
