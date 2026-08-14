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
