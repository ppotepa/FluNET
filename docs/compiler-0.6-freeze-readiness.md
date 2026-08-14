# FluNET 0.6 data-language freeze readiness

This is a source-level freeze candidate, not a release claim. The public built-in language version remains unchanged until a real Release build and test run validates the exact tree.

## Implemented data-language surface

- compact resource loading and deterministic resource/name/format inference;
- automatic dependency graph construction from data flow and effect metadata;
- property/index access and string interpolation;
- explicit and implicit pipelines;
- FILTER, SORT, TAKE, SELECT, MAP and DEFAULT over typed `List<Json>` values;
- `??` coalescing in the shared expression AST;
- bounded `FOR EACH ... PARALLEL n` iteration actions;
- GROUP, SUM, JOIN and MATCH typed data transforms;
- schema inference from explicit JSON samples into structural `Object`, `List`, `Optional` and `Union` language types.

## Schema rules

Schema inference is side-effect free. It never opens a file or performs an HTTP request. A host/provider supplies a sample or a declared schema. All supplied records participate in inference: missing fields are optional, null values produce nullable types, and heterogeneous values produce unions. Stable schema `TypeId` values are derived from canonical field/type structure rather than example values.

## Freeze gate

Before changing any public `LanguageVersion` to claim 0.6, run against this exact tree:

```text
dotnet restore FluNET.sln
dotnet build FluNET.sln --configuration Release --no-restore
dotnet test FluNET.sln --configuration Release --no-build
```

No version or release tag should be created without that verification.
