using FluNET.Capabilities;
using FluNET.Compilation;
using FluNET.Execution.Commands;
using FluNET.Execution.Planning;
using FluNET.Execution.Workflow;
using FluNET.Extensions;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Telemetry;
using FluNET.Variables;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Context;

internal static class FluNetDefaultServiceRegistration
{
    public static void Configure(IServiceCollection services, FluNetRuntimeDefinition runtime)
    {
        services.AddSingleton(runtime.Language);
        RegisterCapabilities(services);
        RegisterCompilerAndExecution(services, runtime);
    }

    private static void RegisterCapabilities(IServiceCollection services)
    {
        services.AddSingleton<CapabilityRegistry>(provider =>
        {
            CapabilityRegistry registry = new();
            registry.Register(provider.GetRequiredService<FileScanCapabilityProvider>());
            registry.Register(provider.GetRequiredService<FileMetadataIndexCapabilityProvider>());
            registry.Register(provider.GetRequiredService<ProviderPackageCapabilityProvider>());
            registry.Register(provider.GetRequiredService<FileSearchCapabilityProvider>());
            registry.Register(provider.GetRequiredService<FileHashCapabilityProvider>());
            registry.Register(provider.GetRequiredService<SystemInfoCapabilityProvider>());
            registry.Register(provider.GetRequiredService<PathCapabilityProvider>());
            registry.Register(provider.GetRequiredService<FileOperationsCapabilityProvider>());
            registry.Register(provider.GetRequiredService<FileTrashCapabilityProvider>());
            registry.Register(provider.GetRequiredService<FileWatchCapabilityProvider>());
            registry.Register(provider.GetRequiredService<KeyValueStorageCapabilityProvider>());
            registry.Register(provider.GetRequiredService<BlobStorageCapabilityProvider>());
            registry.Register(provider.GetRequiredService<TimeCapabilityProvider>());
            registry.Register(provider.GetRequiredService<SqlQueryCapabilityProvider>());
            registry.Register(provider.GetRequiredService<ProcessExecutionCapabilityProvider>());
            registry.Register(provider.GetRequiredService<ArchiveCapabilityProvider>());
            registry.Register(provider.GetRequiredService<NetworkCapabilityProvider>());
            registry.Register(provider.GetRequiredService<ClipboardCapabilityProvider>());
            registry.Register(provider.GetRequiredService<DirectoryOperationsCapabilityProvider>());
            registry.Register(provider.GetRequiredService<EnvironmentCapabilityProvider>());
            registry.Register(provider.GetRequiredService<EnvironmentWriteCapabilityProvider>());
            registry.Register(provider.GetRequiredService<SecretCapabilityProvider>());
            registry.Register(provider.GetRequiredService<NotificationCapabilityProvider>());
            registry.Register(provider.GetRequiredService<MessageBusCapabilityProvider>());
            registry.Register(provider.GetRequiredService<TemporaryArtifactsCapabilityProvider>());
            foreach (ICapabilityProvider capability in provider.GetServices<ICapabilityProvider>())
                registry.Register(capability);
            return registry;
        });

        services.AddSingleton<IExecutionPolicy, AllowAllExecutionPolicy>();
        services.AddSingleton<IFluNetFileSystem, PhysicalFluNetFileSystem>();
        services.AddSingleton<IFluNetFileEnumerator, PhysicalFluNetFileEnumerator>();
        services.AddSingleton<FileScanCapabilityProvider>();
        services.AddSingleton<IFluNetFileMetadataIndex, PhysicalFluNetFileMetadataIndex>();
        services.AddSingleton<FileMetadataIndexCapabilityProvider>();
        services.AddSingleton<IFluNetProviderPackageCatalog, InMemoryFluNetProviderPackageCatalog>();
        services.AddSingleton<ProviderPackageCapabilityProvider>();
        services.AddSingleton<IFluNetFileSearcher, PhysicalFluNetFileSearcher>();
        services.AddSingleton<FileSearchCapabilityProvider>();
        services.AddSingleton<IFluNetFileHasher, PhysicalFluNetFileHasher>();
        services.AddSingleton<FileHashCapabilityProvider>();
        services.AddSingleton<IFluNetSystemInfoProvider, PhysicalFluNetSystemInfoProvider>();
        services.AddSingleton<SystemInfoCapabilityProvider>();
        services.AddSingleton<IFluNetPathResolver, PhysicalFluNetPathResolver>();
        services.AddSingleton<PathCapabilityProvider>();
        services.AddSingleton<IFluNetFileOperations, PhysicalFluNetFileOperations>();
        services.AddSingleton<FileOperationsCapabilityProvider>();
        services.AddSingleton<IFluNetFileTrash, PortableFluNetFileTrash>();
        services.AddSingleton<IFluNetDirectoryTrash>(provider =>
            (IFluNetDirectoryTrash)provider.GetRequiredService<IFluNetFileTrash>());
        services.AddSingleton<IFluNetFileRestore>(provider =>
            (IFluNetFileRestore)provider.GetRequiredService<IFluNetFileTrash>());
        services.AddSingleton<IFluNetDirectoryRestore>(provider =>
            (IFluNetDirectoryRestore)provider.GetRequiredService<IFluNetFileTrash>());
        services.AddSingleton<FileTrashCapabilityProvider>();
        services.AddSingleton<IFluNetFileWatcher, PhysicalFluNetFileWatcher>();
        services.AddSingleton<FileWatchCapabilityProvider>();
        services.AddSingleton<IFluNetKeyValueStore, InMemoryFluNetKeyValueStore>();
        services.AddSingleton<KeyValueStorageCapabilityProvider>();
        services.AddSingleton<IFluNetBlobStore, InMemoryFluNetBlobStore>();
        services.AddSingleton<BlobStorageCapabilityProvider>();
        services.AddSingleton<IFluNetClock, SystemFluNetClock>();
        services.AddSingleton<IFluNetDelay, SystemFluNetDelay>();
        services.AddSingleton<TimeCapabilityProvider>();
        services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromMinutes(5) });
        services.AddSingleton<IHttpTransport, HttpTransport>();
        services.AddSingleton<NetworkCapabilityProvider>();
        services.AddSingleton<IHttpJsonPaginator, HttpJsonPaginator>();
        services.AddSingleton<HttpPaginationCapabilityProvider>();
        services.AddSingleton<IFluNetEventSink, HttpWebhookEventSink>();
        services.AddSingleton<EventSinkCapabilityProvider>();
        services.AddSingleton<IFluNetClipboard, SystemFluNetClipboard>();
        services.AddSingleton<IFluNetClipboardWriter>(provider =>
            provider.GetRequiredService<IFluNetClipboard>() as IFluNetClipboardWriter
            ?? new DenyFluNetClipboardWriter());
        services.AddSingleton<ClipboardCapabilityProvider>();
        services.AddSingleton<IFluNetDirectoryOperations, PhysicalFluNetDirectoryOperations>();
        services.AddSingleton<DirectoryOperationsCapabilityProvider>();
        services.AddSingleton<IEnvironmentReader, ProcessEnvironmentReader>();
        services.AddSingleton<EnvironmentCapabilityProvider>();
        services.AddSingleton<IFluNetConfiguration, EmptyFluNetConfiguration>();
        services.AddSingleton<ConfigurationCapabilityProvider>();
        services.AddSingleton<IEnvironmentWritePolicy, DenyAllEnvironmentWritePolicy>();
        services.AddSingleton<IEnvironmentWriter, DenyEnvironmentWriter>();
        services.AddSingleton<EnvironmentWriteCapabilityProvider>();
        services.AddSingleton<IProcessEnvironmentPolicy, AllowAllProcessEnvironmentPolicy>();
        services.AddSingleton<IHttpAuthenticationScheme, BearerHttpAuthenticationScheme>();
        services.AddSingleton<IAuthenticatedHttpTransport, AuthenticatedHttpTransport>();
        services.AddSingleton<ISqlQueryExecutor, DenySqlQueryExecutor>();
        services.AddSingleton<SqlQueryCapabilityProvider>();
        services.AddSingleton<IFluNetProcessRunner, DenyFluNetProcessRunner>();
        services.AddSingleton<IFluNetProcessSessionRegistry, DenyFluNetProcessSessionRegistry>();
        services.AddSingleton<ProcessExecutionCapabilityProvider>();
        services.AddSingleton<IFluNetArchive, PortableFluNetArchive>();
        services.AddSingleton<ArchiveCapabilityProvider>();
        services.AddSingleton<ITextOutput, ConsoleTextOutput>();
        services.AddSingleton<IFluNetNotifier, ConsoleFluNetNotifier>();
        services.AddSingleton<NotificationCapabilityProvider>();
        services.AddSingleton<IFluNetMessageBus, InMemoryFluNetMessageBus>();
        services.AddSingleton<MessageBusCapabilityProvider>();
        services.AddSingleton<IFluNetTemporaryArtifacts, PhysicalFluNetTemporaryArtifacts>();
        services.AddSingleton<TemporaryArtifactsCapabilityProvider>();
        services.AddSingleton<IEmailTransport, DiagnosticEmailTransport>();
        services.AddSingleton<IWorkflowStateStore, InMemoryWorkflowStateStore>();
        services.AddSingleton<IWorkflowValueSerializer, JsonWorkflowValueSerializer>();
        services.AddSingleton<IFluNetTelemetrySink>(NullFluNetTelemetrySink.Instance);
        services.AddSingleton<IExecutionResultCache, InMemoryExecutionResultCache>();
        services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        services.AddSingleton<ISecretStore, EmptySecretStore>();
        services.AddSingleton<ISecretAccessPolicy, DenyAllSecretAccessPolicy>();
        services.AddSingleton<SecretCapabilityProvider>();
    }

    private static void RegisterCompilerAndExecution(
        IServiceCollection services,
        FluNetRuntimeDefinition runtime)
    {
        services.AddSingleton<SemanticCommandBinder>();
        runtime.RegisterRoutes(services);
        services.AddTransient<CommandDispatcher>();
        services.AddTransient<TypedProgramCompiler>();
        services.AddTransient<TypedProgramTypeValidator>();
        services.AddSingleton<ExecutionPlanner>();
        services.AddTransient<Execution.Planning.SentenceExecutor>();
        services.AddPatternMatchers();
        services.AddScoped<IVariableResolver, VariableResolver>();
        services.AddTransient<Execution.ExecutionPipelineFactory>();
        services.AddTransient<Engine>(provider => new Engine(
            provider.GetRequiredService<IVariableResolver>(),
            provider.GetRequiredService<Execution.ExecutionPipelineFactory>(),
            provider.GetRequiredService<SemanticCommandBinder>(),
            provider.GetRequiredService<ExecutionPlanner>(),
            provider.GetRequiredService<LanguageSnapshot>()));
    }
}
