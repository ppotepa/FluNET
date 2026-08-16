using System.Collections.ObjectModel;

namespace FluNET.Capabilities;

public enum FluNetPlatform
{
    Any,
    Windows,
    Linux,
    MacOS
}

public static class FluNetPlatformInfo
{
    public static FluNetPlatform Current =>
        OperatingSystem.IsWindows() ? FluNetPlatform.Windows :
        OperatingSystem.IsLinux() ? FluNetPlatform.Linux :
        OperatingSystem.IsMacOS() ? FluNetPlatform.MacOS :
        FluNetPlatform.Any;
}

public sealed record CapabilityDescriptor(
    string Id,
    string Version,
    IReadOnlyList<FluNetPlatform> Platforms,
    IReadOnlyList<string> Permissions)
{
    public CapabilityDescriptor(
        string id,
        string version = "1.0",
        IEnumerable<FluNetPlatform>? platforms = null,
        IEnumerable<string>? permissions = null)
        : this(
            NormalizeId(id),
            string.IsNullOrWhiteSpace(version) ? "1.0" : version.Trim(),
            new ReadOnlyCollection<FluNetPlatform>((platforms ?? [FluNetPlatform.Any]).Distinct().ToArray()),
            new ReadOnlyCollection<string>((permissions ?? []).Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()))
    {
    }

    public bool Supports(FluNetPlatform platform) =>
        Platforms.Contains(FluNetPlatform.Any) || Platforms.Contains(platform);

    private static string NormalizeId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim().ToLowerInvariant();
    }
}

public interface ICapabilityProvider
{
    CapabilityDescriptor Descriptor { get; }
    bool IsAvailable { get; }
}

public sealed record CapabilityResolution(
    CapabilityDescriptor Descriptor,
    ICapabilityProvider Provider);

/// <summary>
/// Provider-neutral registry used for capability discovery and platform
/// selection. Command handlers may depend on a typed capability contract,
/// while hosts use this registry to explain availability and permissions.
/// </summary>
public sealed class CapabilityRegistry
{
    private readonly Dictionary<string, List<ICapabilityProvider>> providers =
        new(StringComparer.OrdinalIgnoreCase);

    public void Register(ICapabilityProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        string id = provider.Descriptor.Id;
        if (!providers.TryGetValue(id, out List<ICapabilityProvider>? entries))
        {
            entries = [];
            providers.Add(id, entries);
        }

        entries.RemoveAll(existing => existing.GetType() == provider.GetType());
        entries.Add(provider);
    }

    public IReadOnlyList<CapabilityDescriptor> Describe() =>
        providers.Values
            .SelectMany(items => items)
            .Select(provider => provider.Descriptor)
            .OrderBy(descriptor => descriptor.Id, StringComparer.Ordinal)
            .ToArray();

    public bool TryResolve(
        string id,
        out CapabilityResolution? resolution,
        FluNetPlatform? platform = null)
    {
        resolution = null;
        if (!providers.TryGetValue(id.Trim(), out List<ICapabilityProvider>? entries))
            return false;

        FluNetPlatform target = platform ?? FluNetPlatformInfo.Current;
        ICapabilityProvider? provider = entries
            .Where(item => item.IsAvailable && item.Descriptor.Supports(target))
            .OrderBy(item => item.Descriptor.Platforms.Contains(target) ? 0 : 1)
            .FirstOrDefault();
        if (provider is null)
            return false;

        resolution = new CapabilityResolution(provider.Descriptor, provider);
        return true;
    }

    public CapabilityResolution Require(string id, FluNetPlatform? platform = null) =>
        TryResolve(id, out CapabilityResolution? resolution, platform)
            ? resolution!
            : throw new CapabilityUnavailableException(
                $"Capability '{id}' is not available on {platform ?? FluNetPlatformInfo.Current}.");
}

public sealed class CapabilityUnavailableException(string message) : Exception(message);
