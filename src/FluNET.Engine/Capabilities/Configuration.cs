namespace FluNET.Capabilities;

using System.Text.Json;

public interface IFluNetConfiguration
{
    bool TryGet(string key, out string? value);
}

public sealed class EmptyFluNetConfiguration : IFluNetConfiguration
{
    public bool TryGet(string key, out string? value) { value = null; return false; }
}

public sealed class DictionaryFluNetConfiguration(IReadOnlyDictionary<string, string> values) : IFluNetConfiguration
{
    private readonly IReadOnlyDictionary<string, string> values = values ?? throw new ArgumentNullException(nameof(values));
    public bool TryGet(string key, out string? value) => values.TryGetValue(key, out value);
}

public sealed class EnvironmentFluNetConfiguration(string prefix = "FLUNET_CONFIG_") : IFluNetConfiguration
{
    public string Prefix { get; } = prefix ?? throw new ArgumentNullException(nameof(prefix));
    public bool TryGet(string key, out string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        value = Environment.GetEnvironmentVariable(Prefix + key);
        return value is not null;
    }
}

public sealed class JsonFileFluNetConfiguration : IFluNetConfiguration
{
    private readonly string path;
    private readonly IExecutionPolicy policy;

    public JsonFileFluNetConfiguration(string path, IExecutionPolicy policy)
    {
        this.path = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public bool TryGet(string key, out string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        policy.EnsureFileAccess(path);
        if (!File.Exists(path)) { value = null; return false; }
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement current = document.RootElement;
        foreach (string segment in key.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current.ValueKind != JsonValueKind.Object || !TryGetPropertyIgnoreCase(current, segment, out current))
            {
                value = null;
                return false;
            }
        }
        value = current.ValueKind == JsonValueKind.String ? current.GetString() : current.GetRawText();
        return value is not null;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement objectElement, string name, out JsonElement value)
    {
        if (objectElement.TryGetProperty(name, out value)) return true;
        foreach (JsonProperty property in objectElement.EnumerateObject())
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) { value = property.Value; return true; }
        value = default;
        return false;
    }
}

public sealed class ConfigurationCapabilityProvider(IFluNetConfiguration configuration) : ICapabilityProvider
{
    public CapabilityDescriptor Descriptor { get; } = new(
        "system.configuration", "1.0", [FluNetPlatform.Any], ["configuration.read"]);
    public bool IsAvailable => configuration is not EmptyFluNetConfiguration;
}
