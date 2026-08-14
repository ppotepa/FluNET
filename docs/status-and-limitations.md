# Status and limitations

This is the current source support matrix for `main`. Public `StandardLanguageIdentity.Version` is still **0.3** because the exact production-readiness tree has not passed the formal Release gate.

## Implemented source capabilities

| Area | Status |
| --- | --- |
| typed compiler/type system/conversion graph | implemented |
| canonical AND/THEN/IF/ELSE and execution policies | implemented |
| compact syntax, `,`, `;`, `|`, inference/lowering | implemented |
| JSON/Text/CSV/XML/Binary/Image resources | implemented |
| typed HTTP, SQL, env, secret, AUTH | implemented; SQL/secret need host capability configuration |
| data transforms + bounded nested FOR EACH actions | implemented |
| TASK, POLICY, backoff/jitter/status matchers | implemented |
| CACHE / ONCE BY + durable stores | implemented; durable variants are opt-in |
| EVERY/WATCH/WHEN + calendar/cron | implemented host-driven automation |
| ENSURE | implemented source compiler/runner |
| durable workflow journal/history | implemented single-host stores |
| SYNC desired/observed reconciliation | implemented source candidate |
| durable reconciliation baselines | implemented single-host store |
| three-way conflicts + explicit conflict policies | implemented |
| generic reconciliation mutator registry | implemented; built-in concrete mutator is local JSON replacement |
| WATCH-triggered reconciliation | implemented host-driven signal bridge |
| compensation + saga | explicit local SAVE inverse; arbitrary external rollback intentionally unsupported |
| reconciliation leases/fencing | in-memory and shared-filesystem implementations |
| crash/restart reconciliation checkpoints | implemented; restart re-observes instead of blindly replaying mutation |
| atomic physical file replacement | implemented for built-in physical writes |
| secure host profile | opt-in; HTTPS, allow-list, DNS/private-IP, redirect and path hardening |
| telemetry | opt-in metadata-only sink; no OpenTelemetry dependency in core |
| `FluNET.Tool` | in solution, `net9.0`, configured as local .NET tool preview |
| stress/property/backward-compatibility contracts | source tests added, not yet executed for exact tree |

## Intentional boundaries

- There is still one typed command execution stack; reconciliation mutators produce ordinary plans.
- The built-in reconciliation mutator currently owns local JSON replacement. HTTP/SQL/custom targets should register explicit `IReconciliationMutator` implementations.
- Shared-filesystem fencing is only useful when external/custom mutators propagate and enforce the exposed fencing token where their target supports it.
- Secure hosting is opt-in so compatibility hosts are not silently broken.
- Built-in durable state is single-host/shared-filesystem oriented, not a consensus database or distributed transaction manager.
- Compensation is a saga-style inverse contract, not ACID rollback.

## Verification status

Batches 76–88 form an **IMPLEMENTED RC SOURCE CANDIDATE / NOT VERIFIED**. GitHub status absence or static inspection is not a passing gate.

Canonical gate:

```bash
./scripts/verify-release.sh
```

or:

```powershell
./scripts/verify-release.ps1
```

No language/package version promotion or release tag should occur without successful evidence for the exact tree.
