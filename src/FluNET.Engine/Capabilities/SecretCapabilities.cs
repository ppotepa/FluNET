namespace FluNET.Capabilities;

/// <summary>Opaque secret value. It never formats itself as plaintext.</summary>
public sealed class SecretValue
{
    private readonly string _value;
    private SecretValue(string value) => _value = value ?? throw new ArgumentNullException(nameof(value));
    public static SecretValue Create(string value) => new(value);
    public string Reveal() => _value;
    public override string ToString() => "<secret>";
}

public interface ISecretStore
{
    bool TryGet(string name, out SecretValue? value);
}

public sealed class EmptySecretStore : ISecretStore
{
    public bool TryGet(string name, out SecretValue? value) { value = null; return false; }
}

public sealed class DictionarySecretStore : ISecretStore
{
    private readonly IReadOnlyDictionary<string, SecretValue> _values;
    public DictionarySecretStore(IReadOnlyDictionary<string, string> values)
    {
        _values = values.ToDictionary(item => item.Key, item => SecretValue.Create(item.Value), StringComparer.OrdinalIgnoreCase);
    }
    public bool TryGet(string name, out SecretValue? value) => _values.TryGetValue(name, out value);
}

/// <summary>Reads secrets from host environment variables without exposing values in diagnostics.</summary>
public sealed class EnvironmentSecretStore(string prefix = "FLUNET_SECRET_") : ISecretStore
{
    public string Prefix { get; } = ValidatePrefix(prefix);

    public bool TryGet(string name, out SecretValue? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string? raw = Environment.GetEnvironmentVariable(Prefix + name);
        value = string.IsNullOrEmpty(raw) ? null : SecretValue.Create(raw);
        return value is not null;
    }

    private static string ValidatePrefix(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Any(ch => ch is '\r' or '\n')) throw new ArgumentException("Secret prefix cannot contain newlines.", nameof(value));
        return value;
    }
}

/// <summary>Composes host-owned secret stores in priority order.</summary>
public sealed class CompositeSecretStore(IEnumerable<ISecretStore> stores) : ISecretStore
{
    private readonly ISecretStore[] stores = stores?.Where(store => store is not null).ToArray()
        ?? throw new ArgumentNullException(nameof(stores));

    public bool TryGet(string name, out SecretValue? value)
    {
        foreach (ISecretStore store in stores)
            if (store.TryGet(name, out value) && value is not null) return true;
        value = null;
        return false;
    }
}

public interface ISecretAccessPolicy
{
    void EnsureSecretAccess(string name);
}

public sealed class DenyAllSecretAccessPolicy : ISecretAccessPolicy
{
    public void EnsureSecretAccess(string name) => throw new CapabilityDeniedException($"Secret access is not allowed: {name}");
}

public sealed class AllowListedSecretAccessPolicy(IEnumerable<string> names) : ISecretAccessPolicy
{
    private readonly HashSet<string> _names = new(names, StringComparer.OrdinalIgnoreCase);
    public void EnsureSecretAccess(string name)
    {
        if (!_names.Contains(name)) throw new CapabilityDeniedException($"Secret access is not allowed: {name}");
    }
}

public sealed class SecretCapabilityProvider(ISecretStore store, ISecretAccessPolicy policy) : ICapabilityProvider
{
    public CapabilityDescriptor Descriptor { get; } = new(
        "system.secrets",
        "1.0",
        [FluNetPlatform.Any],
        ["secret.read"]);

    public bool IsAvailable => store is not EmptySecretStore && policy is not DenyAllSecretAccessPolicy;
}
