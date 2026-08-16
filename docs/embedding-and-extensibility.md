# Embedding and extensibility

FluNET.Engine is designed to be embedded in a .NET host. The host chooses the language runtime, capabilities, storage and external transports. This page shows the main public extension points available in the current source tree.

## Canonical engine

For a batteries-included cross-platform application host:

```csharp
using FluNET.Context;

using FluNETContext context = FluNetHost.Create(new FluNetHostOptions
{
    Root = "./workspace",
    DataDirectory = "./.flunet",
    NetworkHosts = ["api.example.test"]
});
```

This wires policy-aware local storage, SQLite metadata index, durable queue,
blob directory and provider-package catalog. Every provider remains replaceable
through the optional `IServiceCollection` callback.

For a runnable reference host, see `samples/FluNET.HostedApp`. It accepts a
workspace root and a canonical prompt, making it a compact starting point for
desktop, service, worker or container applications.

The engine is packable as `FluNET.Engine` (`0.9.0` candidate). The package
contains the README and declares the MIT license metadata so an embedding host
can consume the runtime without copying repository files.

```csharp
using FluNET;
using FluNET.Context;
using FluNET.Prompt;

using FluNETContext context = FluNETContext.Create();
Engine engine = context.GetEngine();

ExecutionResult result = await engine.ExecuteAsync(
    new ProcessedPrompt("SAY 'Hello'."));
```

Source-compatible analysis:

```csharp
CompilationResult analysis = engine.Analyze(
    new ProcessedPrompt("GET [text] FROM {input.txt}."));
```

Typed side-effect-free analysis:

```csharp
TypedAnalysisResult typed = context.AnalyzeTyped(
    new ProcessedPrompt(
        "SET NUMBER [count] TO 42 THEN SAY [count]."));
```

## Compact/surface context

Create the standard + surface runtime:

```csharp
using FluNET.Context;

using FluNETContext context =
    SurfaceCompilationExtensions.CreateSurfaceContext();
```

Compile without execution:

```csharp
SurfaceCompilationResult compilation = context.CompileSurface("""
LOAD post.json, todo.json
SAY "{post.title} — {todo.title}"
""");
```

Execute through the same `SentenceExecutor` used by the rest of the engine:

```csharp
SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync("""
GET https://api.example.test/posts/1 AS post
SAY "{post.title}"
""");
```

`SurfaceCompilationResult` exposes the major intermediate artifacts:

```text
Document
SurfaceParse
Lowering
  CanonicalSyntax
  SourceMap
  InferenceTrace
BoundProgram
TypedProgram
DependencyGraph
ExecutionPlan
Diagnostics / FailedPhase
```

## Override capabilities and transports

```csharp
using FluNET.Capabilities;
using Microsoft.Extensions.DependencyInjection;

using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(
    services =>
    {
        services.AddSingleton<IExecutionPolicy>(myPolicy);
        services.AddSingleton<IHttpTransport>(myHttpTransport);
        // Choose the host's authentication convention: Bearer, API key, or Basic.
        services.AddSingleton<IHttpAuthenticationScheme>(new ApiKeyHttpAuthenticationScheme("X-API-Key"));
        services.AddSingleton<IFluNetFileSystem>(myFileSystem);
        services.AddSingleton<ITextOutput>(myOutput);
    services.AddSingleton<IEmailTransport>(myEmailTransport);
    });
```

For a host-owned remote queue, the standard messaging surface can use the
portable REST adapter:

```csharp
services.AddSingleton<IFluNetMessageBus>(provider =>
    new HttpFluNetMessageBus(
        new Uri("https://queue.example.test/events"),
        provider.GetRequiredService<IHttpTransport>(),
        provider.GetRequiredService<IAuthenticatedHttpTransport>()));
```

The adapter uses `POST /topic` for publish and `GET /topic` for receive; a
broker-specific host can implement `IFluNetMessageBus` directly instead.

The default embedding `FluNETContext` keeps `AllowAllExecutionPolicy` for backward compatibility. Production hosts should install an appropriate policy.

## Provider package catalog

Hosts can publish approved provider manifests without changing the language
runtime:

```csharp
services.AddSingleton<IFluNetProviderPackageCatalog>(
    new JsonFileFluNetProviderPackageCatalog(
        "./.flunet/packages",
        myPolicy));
```

The `PACKAGES [packages]` command exposes only validated metadata. Loading an
entry point and granting permissions remains an explicit host decision.

## Secrets

The default host uses:

```text
ISecretStore        = EmptySecretStore
ISecretAccessPolicy = DenyAllSecretAccessPolicy
```

Install an explicit store and allow-list:

```csharp
using FluNET.Capabilities;
using Microsoft.Extensions.DependencyInjection;

using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(
    services =>
    {
        services.AddSingleton<ISecretStore>(new DictionarySecretStore(
            new Dictionary<string, string>
            {
                ["github-token"] = "..."
            }));

        services.AddSingleton<ISecretAccessPolicy>(
            new AllowListedSecretAccessPolicy(["github-token"]));
    });
```

Compact source can then request:

```text
GET secret:github-token AS token
```

`SecretValue` remains opaque. Its `ToString()` returns `<secret>` and the language has no implicit Secret -> Text conversion.

## Durable workflow journals

```csharp
using FluNET.Execution.Workflow;

using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(
    services =>
    {
        services.AddDurableFluNetWorkflows(".flunet/workflows");
    });
```

The durable workflow implementation uses the existing `IWorkflowStateStore` protocol, so resume behavior does not split into a second execution model.

See [Durable workflows](durable-workflows.md).

## Cache and idempotency stores

Defaults:

```text
IExecutionResultCache = InMemoryExecutionResultCache
IIdempotencyStore     = InMemoryIdempotencyStore
```

A host can replace either interface through DI. This is recommended when CACHE or `ONCE BY` semantics need to survive a process restart.

## Compile automations

```csharp
using FluNET.Automation;

AutomationCompilationResult automation = context.CompileAutomations("""
EVERY 1h
    GET https://api.example.test/status AS status
    SAY "Status: {status.name}"
""");
```

The compiler returns trigger metadata plus normal precompiled workflow templates. See [Automation and desired state](automation-and-desired-state.md) for scheduler usage.

## Compile and execute ENSURE

Compile:

```csharp
DesiredStateCompilationResult desired = context.CompileEnsure("""
ENSURE backup.json CONTAINS https://api.example.test/config
KEEP 7 VERSIONS
""");
```

Execute:

```csharp
IReadOnlyList<EnsureRunResult> runs = await context.ExecuteEnsureAsync("""
ENSURE backup.json CONTAINS https://api.example.test/config
KEEP 7 VERSIONS
NOTIFY ON FAILURE
""");
```

Optional durable version retention for local targets:

```csharp
using FluNET.Declarative;

using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(
    services => services.AddDirectoryEnsureVersions(
        ".flunet/ensure-versions"));
```

## Build a custom language module

Native modules declare stable frame identity independently of CLR class names:

```csharp
public sealed class ReportingModule : IFluNetModule
{
    public void Register(FluNetModuleBuilder module)
    {
        module.Language.Module("reporting");

        module.Command<GenerateReportCommand, FileInfo>(
                "GENERATE",
                "Report")
            .FrameId("reporting.generate")
            .Aliases("BUILD")
            .Positional<FileInfo>(
                SemanticRole.Output,
                SlotDirection.Output)
            .Marked<DateOnly>(SemanticRole.Source, "FROM")
            .BindWith<GenerateReportBinder>()
            .HandleWith<GenerateReportHandler>();
    }
}
```

Combine modules into one validated runtime:

```csharp
FluNetRuntimeDefinition runtime = new FluNetModuleBuilder()
    .AddModule(new StandardLanguageModule())
    .AddModule(new SurfaceLanguageModule())
    .AddModule(new ReportingModule())
    .Build();

using FluNETContext context =
    FluNETContext.CreateWithRuntime(runtime);
```

The runtime validates stable frame/routes before the host starts.

## Custom language types, codecs and conversions

Declare a stable domain type:

```csharp
module.Language.Type<Slug>("Slug");
```

Register its parser/formatter boundary:

```csharp
module.Codec<Slug, SlugCodec>();
```

Register an explicit conversion:

```csharp
module.Conversion<Slug, string, SlugToTextConversion>();
```

Optional conversion configuration includes `ConversionKind` and positive path cost. The compiler/runtime resolves conversions through `IValueCodecRegistry`; type assignability itself remains structural.

## Custom resource providers

Compact `GET`/`LOAD` lowering is provider-driven. The registry has built-ins for file, HTTP JSON, environment and secrets; modules can register additional providers:

```csharp
module.ResourceProvider<MyResourceProvider>();
```

Provider contract:

```csharp
public sealed class MyResourceProvider : IResourceProvider
{
    public string Id => "example.resource";

    public IReadOnlyList<ResourceCapability> RequiredCapabilities =>
        [ResourceCapability.DatabaseRead];

    public bool CanHandle(ResourceDescriptor descriptor) =>
        descriptor.Reference is ModuleResourceReference resource &&
        resource.Scheme.Equals(
            "example",
            StringComparison.OrdinalIgnoreCase);

    public ResourceProviderResult LowerRead(
        ResourceProviderContext context)
    {
        // Return canonical CommandSyntax nodes or a stable diagnostic.
        throw new NotImplementedException();
    }
}
```

Portable host composition is also available for environment-backed secrets:

```csharp
services.AddSingleton<ISecretStore>(new CompositeSecretStore([
    new EnvironmentSecretStore("APP_SECRET_"),
    new DictionarySecretStore(testFallbacks)
]));
```

`CompositeSecretStore` checks stores in order. Secret values remain opaque;
their `ToString()` representation is always `<secret>`.

`IHttpAuthenticationScheme` is deliberately host-owned. The built-in schemes are
`BearerHttpAuthenticationScheme`, `ApiKeyHttpAuthenticationScheme` and
`BasicHttpAuthenticationScheme`; custom schemes can implement the same interface
without changing the language or compiler.

## Module capabilities

Modules can also ship a provider-neutral capability. This keeps the language
surface portable while allowing the host to decide whether the provider is
available, which policy it uses, and which platform-specific implementation it
injects:

```csharp
public sealed class ReportsModule : IFluNetModule
{
    public void Register(FluNetModuleBuilder module)
    {
        module.Capability<ReportsCapabilityProvider>();
    }
}

public sealed class ReportsCapabilityProvider(IReportStore store)
    : ICapabilityProvider
{
    public CapabilityDescriptor Descriptor { get; } = new(
        "reports.store",
        platforms: [FluNetPlatform.Any],
        permissions: ["reports.read", "reports.write"]);

    public bool IsAvailable => store.IsAvailable;
}
```

`CapabilityRegistry` discovers module providers together with the built-in
filesystem, storage, network, process and system providers. The registry only
exposes an available provider for the current platform; it never chooses a
platform-specific implementation by inspecting application code.

Unknown `scheme:value` resources are represented by `ModuleResourceReference`, so a provider can own a scheme without changing `SurfaceLowerer`.

Providers declare capability categories (`FileRead`, `NetworkRead`, `EnvironmentRead`, `SecretRead`, `DatabaseRead`) for host/tooling inspection. The provider still needs to lower into a command frame with an actual typed route/handler.

## Keep side effects behind boundaries

Recommended module design:

```text
surface syntax/inference
        -> provider/lowering
        -> semantic frame
        -> typed binder
        -> typed command
        -> capability-aware handler
```

Do not perform I/O in:

- `SurfaceParser`;
- inference rules;
- type checking;
- dependency analysis;
- lowering that merely decides which canonical command should execute.

## Thread safety

Execution-plan nodes can run concurrently when data/control/effect dependencies allow it. Extension-owned mutable state must therefore be synchronized or scoped appropriately.

## Versioning note

Stable `CommandId`, `FrameId`, `TypeId` and module ids are intended to outlive CLR refactors. The public built-in language version is still `0.3`; do not treat current source-level 0.6/0.7 milestone work as a published compatibility promise until the release gate is completed.
