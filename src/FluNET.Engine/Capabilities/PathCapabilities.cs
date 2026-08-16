namespace FluNET.Capabilities;

public interface IFluNetPathResolver
{
    string Resolve(string name);
}

public sealed class PhysicalFluNetPathResolver : IFluNetPathResolver
{
    public string Resolve(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string normalized = name.Trim().ToUpperInvariant();
        string? value = normalized switch
        {
            "CURRENT" or "CWD" => Environment.CurrentDirectory,
            "TEMP" or "TMP" => Path.GetTempPath(),
            "HOME" or "USER" => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "DESKTOP" => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "DOCUMENTS" or "DOCS" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "DOWNLOADS" => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) is { Length: > 0 } home
                ? Path.Combine(home, "Downloads")
                : null,
            "APPDATA" or "CONFIG" => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LOCALAPPDATA" => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CACHE" => ResolveCacheDirectory(),
            "PROGRAMDATA" => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            _ => null
        };

        if (string.IsNullOrWhiteSpace(value))
            throw new KeyNotFoundException($"Special path '{name}' is not available on this host.");
        return Path.GetFullPath(value);
    }

    private static string ResolveCacheDirectory()
    {
        string? xdg = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        if (!string.IsNullOrWhiteSpace(xdg)) return xdg;
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrWhiteSpace(local) ? Path.GetTempPath() : Path.Combine(local, "Cache");
    }
}

public sealed class PathCapabilityProvider : ICapabilityProvider
{
    public CapabilityDescriptor Descriptor { get; } = new(
        "system.path",
        "1.0",
        [FluNetPlatform.Any],
        ["system.path.read"]);

    public bool IsAvailable => true;
}
