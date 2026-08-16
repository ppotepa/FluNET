using System.Collections.Concurrent;
using System.Text.Json;

namespace FluNET.Capabilities;

public sealed record FluNetProviderPackageManifest(
    string Id,
    string Version,
    string EntryPoint,
    IReadOnlyList<FluNetPlatform> Platforms,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> Permissions)
{
    public FluNetProviderPackageManifest Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(Version);
        ArgumentException.ThrowIfNullOrWhiteSpace(EntryPoint);
        if (Platforms.Count == 0) throw new ArgumentException("A provider package must declare at least one platform.", nameof(Platforms));
        return this with
        {
            Id = Id.Trim().ToLowerInvariant(),
            Version = Version.Trim(),
            EntryPoint = EntryPoint.Trim(),
            Platforms = Platforms.Distinct().ToArray(),
            Capabilities = Capabilities.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim().ToLowerInvariant()).Distinct().ToArray(),
            Permissions = Permissions.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim().ToLowerInvariant()).Distinct().ToArray()
        };
    }

    public bool SupportsCurrentPlatform => Platforms.Contains(FluNetPlatform.Any) || Platforms.Contains(FluNetPlatformInfo.Current);
}

public interface IFluNetProviderPackageCatalog
{
    void Register(FluNetProviderPackageManifest manifest);
    IReadOnlyList<FluNetProviderPackageManifest> Discover(bool currentPlatformOnly = false);
}

public sealed class InMemoryFluNetProviderPackageCatalog : IFluNetProviderPackageCatalog
{
    private readonly ConcurrentDictionary<string, FluNetProviderPackageManifest> packages = new(StringComparer.OrdinalIgnoreCase);

    public void Register(FluNetProviderPackageManifest manifest)
    {
        FluNetProviderPackageManifest validated = (manifest ?? throw new ArgumentNullException(nameof(manifest))).Validate();
        packages[validated.Id] = validated;
    }

    public IReadOnlyList<FluNetProviderPackageManifest> Discover(bool currentPlatformOnly = false) =>
        packages.Values
            .Where(package => !currentPlatformOnly || package.SupportsCurrentPlatform)
            .OrderBy(package => package.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}

public sealed class JsonFileFluNetProviderPackageCatalog : IFluNetProviderPackageCatalog
{
    private readonly string directory;
    private readonly IExecutionPolicy policy;

    public JsonFileFluNetProviderPackageCatalog(string directory, IExecutionPolicy policy)
    {
        this.directory = Path.GetFullPath(directory ?? throw new ArgumentNullException(nameof(directory)));
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public void Register(FluNetProviderPackageManifest manifest)
    {
        FluNetProviderPackageManifest validated = (manifest ?? throw new ArgumentNullException(nameof(manifest))).Validate();
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, validated.Id + ".json");
        policy.EnsureFileAccess(path);
        File.WriteAllText(path, JsonSerializer.Serialize(validated, new JsonSerializerOptions { WriteIndented = true }));
    }

    public IReadOnlyList<FluNetProviderPackageManifest> Discover(bool currentPlatformOnly = false)
    {
        if (!Directory.Exists(directory)) return [];
        List<FluNetProviderPackageManifest> manifests = [];
        foreach (string path in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.OrdinalIgnoreCase))
        {
            policy.EnsureFileAccess(path);
            FluNetProviderPackageManifest? manifest = JsonSerializer.Deserialize<FluNetProviderPackageManifest>(File.ReadAllText(path));
            if (manifest is not null)
            {
                FluNetProviderPackageManifest validated = manifest.Validate();
                if (!currentPlatformOnly || validated.SupportsCurrentPlatform) manifests.Add(validated);
            }
        }
        return manifests.OrderBy(manifest => manifest.Id, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}

public sealed class ProviderPackageCapabilityProvider : ICapabilityProvider
{
    public CapabilityDescriptor Descriptor { get; } = new(
        "ecosystem.provider-packages",
        "1.0",
        [FluNetPlatform.Any],
        ["ecosystem.discovery"]);

    public bool IsAvailable => true;
}
