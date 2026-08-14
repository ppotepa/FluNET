# FluNET 1.0 host security contract candidate

The 1.0 security contract separates language meaning from host authorization. `SurfaceSecurityAnalyzer` can project a compiled program to a `SurfaceSecurityManifest` before effects run.

Capability categories:

- `FileRead`
- `FileWrite`
- `NetworkRead`
- `NetworkWrite`
- `EnvironmentRead`
- `SecretRead`
- `DatabaseRead`
- `TextOutput`
- `EmailSend`

The analyzer also inspects compiled `FOR EACH` action descriptors so nested GET/LOAD/SAVE/POST/SAY operations contribute requirements.

This manifest is advisory/introspective; enforcement remains at the existing capability boundaries (`IExecutionPolicy`, secret policy/store, HTTP/auth transport, SQL executor, etc.). The backwards-compatible embedding default is not silently changed into a sandbox.

Production hosts should compare the manifest with an explicit allow policy before execution and isolate the process where untrusted input is involved.
