# FluNET 1.0 language-contract candidate

This file defines what the 1.0 language freeze is expected to stabilize. It is a candidate contract, not a release declaration; public `StandardLanguageIdentity.Version` remains `0.3` until the Release gate passes.

## Stable identity surfaces

The runtime exposes `LanguageContractManifest.Create(snapshot, version)` so hosts/tests can snapshot stable frame/type identities without depending on CLR implementation class names.

The 1.0 contract includes:

- stable `FrameId` values and result language `TypeId` values;
- structural language types rather than CLR aliases;
- canonical expression precedence;
- separator semantics:
  - `,` = another value/member of the same syntactic role;
  - `;` / newline = neutral compact statement boundaries;
  - `|` = dataflow;
  - canonical `AND` = explicit parallel coordination;
  - canonical `THEN` = explicit ordering/barrier;
- one typed compile/plan/execution pipeline shared by canonical and compact surfaces.

## Explicitly not frozen by this candidate

- private CLR class names;
- diagnostic wording where the diagnostic code/semantic category is unchanged;
- physical package split;
- provider-specific implementation details;
- a public 1.0 version number before verification.

The final 1.0 verification batch must persist the generated manifest for the exact verified tree and compare it in compatibility tests.
