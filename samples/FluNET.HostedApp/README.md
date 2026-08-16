# FluNET.HostedApp

Minimal cross-platform embedding sample. It uses `FluNetHost` to provide a
policy-aware workspace, SQLite metadata index, durable queue, blob directory
and package catalog.

Run from the repository root:

```text
dotnet run --project samples/FluNET.HostedApp -- . "CAPABILITIES [caps]"
dotnet run --project samples/FluNET.HostedApp -- . "DOCTOR [report]"
dotnet run --project samples/FluNET.HostedApp -- . "INDEX FILES [files] FROM {.} RECURSIVE"
```

The first argument is the allowed workspace root. Remaining arguments form a
canonical FluNET prompt. The host can replace any provider through the
`FluNetHost.Create(..., configure)` callback.
