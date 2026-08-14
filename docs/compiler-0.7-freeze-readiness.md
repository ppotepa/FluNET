# FluNET 0.7 automation-language freeze readiness

This is a source-level freeze candidate only. Public language versions remain unchanged until a real Release build/test run verifies the exact tree.

Implemented compiler/runtime contracts now include reusable TASK templates, compile-time policy profiles, pluggable resource providers, execution-result caching, idempotent `ONCE BY` mutations, EVERY/WATCH/WHEN automation templates, and an opaque `Secret` language type with deny-by-default access policy.

Secrets are not Text. There is no implicit Secret-to-Text conversion, and the default host denies secret reads. Providers declare required capability categories, allowing future hosts/tooling to inspect resource effects before execution.

Freeze gate:

```text
dotnet restore FluNET.sln
dotnet build FluNET.sln --configuration Release --no-restore
dotnet test FluNET.sln --configuration Release --no-build
```
