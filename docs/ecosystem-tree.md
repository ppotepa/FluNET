# FluNET ecosystem tree

This is the source-of-truth map for the cross-platform ecosystem. A surface
command must lower into a stable frame, execute through the shared `SentenceExecutor`,
and reach the operating system only through a capability contract.

```text
FluNET
├── Language surface
│   ├── Canonical sentences
│   ├── Compact surface syntax
│   │   ├── resources: GET / LOAD / SAVE / POST / PUT / PATCH / DELETE
│   │   ├── files: SCAN / FIND / SEARCH / LIST [RECURSIVE] / LIST ARCHIVE / HASH / COPY / MOVE / TRASH
│   │   ├── directory transfers: COPY DIRECTORY / MOVE DIRECTORY / TRASH DIRECTORY / RESTORE DIRECTORY
│   │   ├── directories: MKDIR
│   │   ├── archives: PACK / UNPACK
│   │   ├── storage: STORE / READ / LIST STORE / DELETE STORE / LIST BLOB / GET blob: / SAVE TO blob: / DELETE blob:
│   │   ├── system: SYSTEM INFO / PATH TEMP / NOW / WAIT / GET env:NAME / READ CLIPBOARD / COPY TO CLIPBOARD
│   │   ├── processes: EXECUTE / START / SEND / STOP / RUNPROCESS / IN / ENV
│   │   ├── collections: WHERE / ORDER BY / DISTINCT / GROUP / aggregates / SAVE CSV
│   │   ├── text: TRIM / UPPER / LOWER / REPLACE / SPLIT / LINES / COMBINE / EXPECT
│   │   ├── modules: IMPORT / TASK / RUN
│   │   └── flow: IF / UNLESS / ELSE / REPEAT / WHILE / BREAK / CONTINUE / FOR EACH / TASK / POLICY / pipelines
│   └── Automation surface
│       ├── EVERY / CRON
│       ├── WATCH / WHEN
│       └── ENSURE / SYNC
├── Compiler and runtime
│   ├── SourceDocument
│   ├── Lexer
│   ├── SentenceSegmenter → Sentence[]
│   ├── SurfaceParser
│   ├── Inference and resource providers
│   ├── SurfaceLowerer → canonical frames
│   ├── SemanticBinder → typed commands
│   ├── DependencyGraph / ExecutionPlan
│   └── SentenceExecutor → typed handlers
├── Capability contracts
│   ├── Filesystem
│   │   ├── filesystem.scan
│   │   ├── filesystem.search
│   │   ├── filesystem.directory (LIST / STAT / MKDIR / directory transfer)
│   │   ├── filesystem.hash
│   │   ├── filesystem.write (COPY / MOVE)
│   │   ├── filesystem.trash (files and directory trees)
│   │   ├── filesystem.archive
│   │   └── filesystem.watch
│   ├── Storage
│   │   ├── storage.keyvalue (memory / JSON file / SQLite)
│   │   ├── storage.blob (memory / local file root / HTTP object gateway / S3-compatible SigV4 / cloud-provider seam)
│   │   └── database.sql (provider-neutral / SQLite / DbProviderFactory / parameters / APPLY)
│   ├── Network
│   │   ├── network.http (GET / REQUEST / POST / authenticated transport / JSON pagination)
│   │   └── events.sink (EMIT / webhook / host-owned broker seam)
│   ├── System
│   │   ├── system.info / CAPABILITIES / PACKAGES / DOCTOR
│   │   ├── system.metrics
│   │   ├── system.path
│   │   ├── system.temp (temporary files / directories / owned cleanup)
│   │   ├── system.environment (GET env:NAME / SET ENV)
│   │   ├── system.configuration (GET config:NAME / host-owned provider)
│   │   ├── system.clipboard
│   │   ├── system.time (clock / delay)
│   │   ├── system.notify
│   │   └── system.secrets
│   ├── Process
│   │   └── system.process
│   └── Messaging
│   │   └── messaging.queue (PUBLISH / JSONL / HTTP REST / host-owned consumers)
├── Provider model
│   ├── CapabilityDescriptor
│   ├── ICapabilityProvider
│   ├── CapabilityRegistry
│   ├── platform selection (Any / Windows / Linux / macOS)
│   ├── permission metadata
│   └── module registration: FluNetModuleBuilder.Capability<TProvider>()
└── Host integrations
    ├── CLI interactive session / multiline / clipboard paste
    ├── `FluNetHost` batteries-included embedding factory
    ├── CLI tools --json capability discovery
    ├── CLI --root / --host / --store / --sqlite / --allow-process
    ├── automation scheduler and file-watch bridge
    ├── graph / explain / check / format tooling
    ├── provider package manifests / catalog
    └── custom modules, resource providers and host-owned implementations
```

## Design rules

1. Surface syntax is declarative; it never calls `System.IO`, a shell, or a
   platform API directly.
2. Every side effect has a capability contract and is checked by the active
   `IExecutionPolicy`.
3. The default host may expose portable providers while denying dangerous
   capabilities such as secrets, SQL, or process execution.
4. Platform-specific implementations are selected by the host/provider layer,
   not by compiler branches in the language surface.
5. New ecosystem packages should contribute a module, typed routes, a
   capability provider, tests, and a graph/tooling mapping together.

## Next package seams

The remaining expansion points are intentionally provider-shaped:

- richer path and file-query predicates over `LIST`/`FIND` (text/glob operators,
  metadata aliases, natural timestamp comparisons and portable size units are
  implemented; the provider seam remains open for platform-specific indexes);
- additional database adapters behind `ISqlQueryExecutor` (including the
  generic `DbProviderFactory` bridge);
- cloud/object storage providers behind a resource provider;
- provider-backed archive/extract formats beyond the current ZIP/TAR/TAR.GZ capability;
- durable automation event adapters beyond the local JSONL/SQLite signal journals.

These additions do not require another execution engine; they extend the same
frame → command → capability → provider path.
