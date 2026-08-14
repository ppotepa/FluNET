# FluNET 1.0 compatibility candidate

The 1.0 candidate distinguishes **preferred** extension APIs from **legacy-supported** compatibility surfaces without deleting old APIs before verification.

Preferred for new development:

- `IFluNetModule` / `FluNetModuleBuilder`;
- typed commands, binders and handlers;
- `SurfaceCompiler` / typed compilation;
- value codecs/conversions;
- resource providers/decoders/encoders/observers.

Legacy-supported:

- sentence/word/verb object model;
- token-tree representation;
- `LegacySentenceAdapter` and related compatibility bridges.

Legacy-supported means source compatibility is intentionally retained for the candidate, but new feature work does not extend those paths. `CompatibilityContract` is the machine-readable ledger for this distinction.

No bulk API removal is part of this batch; removal would require a separately versioned breaking-change decision after the verified 1.0 contract exists.
