# FluNET 1.0 module-boundary candidate

The 1.0 contract freezes **logical responsibilities and dependency direction**, not the current physical `.csproj` layout.

Logical modules:

- `flunet.core` — canonical grammar/types/typed runtime;
- `flunet.surface` — compact syntax/inference/lowering/tooling;
- `flunet.data` — typed collection/data operations;
- `flunet.automation` — triggers and host-driven scheduling;
- `flunet.reconciliation` — desired/observed state, SYNC, diff, compensation/saga/history orchestration;
- `flunet.providers` — resource acquisition/decoding/auth/observation;
- `flunet.compatibility` — legacy bridges only.

`FluNetPlatformTopology` exposes this topology programmatically. All modules currently remain in `FluNET.Engine` so package splitting cannot destabilize the candidate before build/test verification.

A later physical package split may map these logical modules to separate NuGet packages without changing the language/execution contract. Preferred modules must not acquire dependencies on the compatibility layer.
