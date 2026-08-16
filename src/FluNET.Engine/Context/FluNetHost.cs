using FluNET.Capabilities;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Context;

/// <summary>Small batteries-included host factory for cross-platform FluNET applications.</summary>
public sealed record FluNetHostOptions
{
    public string Root { get; init; } = Directory.GetCurrentDirectory();
    public string DataDirectory { get; init; } = ".flunet";
    public IReadOnlyList<string> NetworkHosts { get; init; } = [];
    public bool OpenNetwork { get; init; }
    public string? ConfigurationPath { get; init; }
    public string? PackageDirectory { get; init; }
    public string? IndexPath { get; init; }
    public string? StorePath { get; init; }
    public string? QueuePath { get; init; }
    public string? BlobDirectory { get; init; }

    public FluNetHostOptions Normalize()
    {
        string root = Path.GetFullPath(Root);
        string data = Path.IsPathRooted(DataDirectory) ? Path.GetFullPath(DataDirectory) : Path.Combine(root, DataDirectory);
        return this with
        {
            Root = root,
            DataDirectory = data,
            PackageDirectory = Path.GetFullPath(PackageDirectory ?? Path.Combine(data, "packages")),
            IndexPath = Path.GetFullPath(IndexPath ?? Path.Combine(data, "index.db")),
            StorePath = Path.GetFullPath(StorePath ?? Path.Combine(data, "store.json")),
            QueuePath = Path.GetFullPath(QueuePath ?? Path.Combine(data, "queue.jsonl")),
            BlobDirectory = Path.GetFullPath(BlobDirectory ?? Path.Combine(data, "blobs")),
            ConfigurationPath = ConfigurationPath is null ? null : Path.GetFullPath(ConfigurationPath)
        };
    }
}

public static class FluNetHost
{
    public static FluNETContext Create(
        FluNetHostOptions? options = null,
        Action<IServiceCollection>? configure = null)
    {
        FluNetHostOptions normalized = (options ?? new FluNetHostOptions()).Normalize();
        return SurfaceCompilationExtensions.CreateSurfaceContext(services =>
        {
            services.AddSingleton<IExecutionPolicy>(new FluNetHostExecutionPolicy(normalized));
            services.AddSingleton<IFluNetKeyValueStore>(CreateKeyValueStore(normalized.StorePath!, normalized));
            services.AddSingleton<IFluNetMessageBus>(CreateMessageBus(normalized.QueuePath!, normalized));
            services.AddSingleton<IFluNetBlobStore>(new FileFluNetBlobStore(normalized.BlobDirectory!, new FluNetHostExecutionPolicy(normalized)));
            services.AddSingleton<IFluNetFileMetadataIndex>(new SqliteFluNetFileMetadataIndex(normalized.IndexPath!, new FluNetHostExecutionPolicy(normalized)));
            services.AddSingleton<IFluNetProviderPackageCatalog>(new JsonFileFluNetProviderPackageCatalog(normalized.PackageDirectory!, new FluNetHostExecutionPolicy(normalized)));
            if (normalized.ConfigurationPath is not null)
                services.AddSingleton<IFluNetConfiguration>(new JsonFileFluNetConfiguration(normalized.ConfigurationPath, new FluNetHostExecutionPolicy(normalized)));
            configure?.Invoke(services);
        });
    }

    private static IFluNetKeyValueStore CreateKeyValueStore(string path, FluNetHostOptions options) =>
        path.EndsWith(".db", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase)
            ? new SqliteFluNetKeyValueStore(path, new FluNetHostExecutionPolicy(options))
            : new JsonFileFluNetKeyValueStore(path, new FluNetHostExecutionPolicy(options));

    private static IFluNetMessageBus CreateMessageBus(string path, FluNetHostOptions options) =>
        path.EndsWith(".db", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase)
            ? new SqliteFluNetMessageBus(path, new FluNetHostExecutionPolicy(options))
            : new JsonFileFluNetMessageBus(path, new FluNetHostExecutionPolicy(options));

    private sealed class FluNetHostExecutionPolicy(FluNetHostOptions options) : IExecutionPolicy
    {
        private readonly RestrictedExecutionPolicy files = new([options.Root], []);

        public void EnsureFileAccess(string path) => files.EnsureFileAccess(path);

        public void EnsureNetworkAccess(Uri uri)
        {
            if (options.OpenNetwork) return;
            if (uri.Scheme is not ("http" or "https") || !options.NetworkHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
                throw new CapabilityDeniedException($"Network access is not allowed: {uri}");
        }
    }
}
