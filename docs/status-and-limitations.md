# Status and limitations

This file is the current support matrix for `main`. Public `StandardLanguageIdentity.Version` is still **0.3** because the exact current tree has not passed the formal Release restore/build/test gate.

## Implemented source capabilities

| Area | Status |
| --- | --- |
| typed compiler/type system/conversion graph | implemented |
| canonical AND/THEN/IF/ELSE and execution policies | implemented |
| compact syntax, `,`, `;`, `|`, inference/lowering | implemented |
| check/fmt/explain/graph/run | implemented |
| JSON/Text/CSV/XML local decoding | implemented |
| Binary/Image values and local decoding | implemented |
| generic typed HTTP media decoding | implemented |
| `env:`, `secret:`, `sql:` providers | implemented; secret/SQL denied until host configures capability |
| lexical `AUTH secret:name` | implemented for HTTP; Bearer is default host scheme |
| FILTER/SORT/TAKE/SELECT/MAP/DEFAULT/GROUP/SUM/JOIN/MATCH | implemented |
| `FOR EACH item IN collection PARALLEL n` | implemented with GET/LOAD/SAVE/POST/SAY nested actions |
| TASK/RUN | implemented compile-time expansion |
| POLICY/WITH | implemented |
| BACKOFF/JITTER/status-specific retry/continue/fail | implemented |
| CACHE / ONCE BY | implemented |
| durable cache/idempotency | opt-in single-host implementation |
| EVERY/WATCH/WHEN | implemented embedding compiler |
| EVERY DAY/weekday AT and CRON | implemented with timezone support |
| durable automation schedule state | implemented single-host store |
| automation CLI | check/run/tick/signal |
| ENSURE | compile/check/apply, refresh/version/failure hooks |
| durable workflow journal | implemented single-host checksummed store |

## Still outside the 0.8 public candidate

The next milestone (0.9) owns generic desired/observed state, resource observation, `SYNC`, keyed diff/reconciliation, WATCH reconciliation, compensation/sagas and audit/history APIs. These should not be inferred from the existing ENSURE foundation.

## Security boundaries

Default embedding policy remains permissive for backwards compatibility. Default secret access is deny-all and SQL is unconfigured/denied. Production hosts should configure file/network/secret/database capabilities explicitly.

## Durability boundaries

Current durable stores target one host. They provide atomic/write-through persistence where documented, but are not distributed transactions or lease systems.

## Verification gate

```bash
dotnet restore FluNET.sln
dotnet build FluNET.sln --configuration Release --no-restore
dotnet test FluNET.sln --configuration Release --no-build
```

No language-version bump or release tag should occur without successful evidence for the exact tree.
