using FluNET.Language;

namespace FluNET.Execution.Capabilities;

public interface ICapabilityPolicy
{
    bool IsAllowed(string capability, VerbDescriptor verb);
}

public sealed class AllowAllCapabilityPolicy : ICapabilityPolicy
{
    public static AllowAllCapabilityPolicy Instance { get; } = new();
    public bool IsAllowed(string capability, VerbDescriptor verb) => true;
}

public sealed class ExplicitCapabilityPolicy(IEnumerable<string> allowed) : ICapabilityPolicy
{
    private readonly HashSet<string> _allowed = new(allowed, StringComparer.OrdinalIgnoreCase);
    public bool IsAllowed(string capability, VerbDescriptor verb) => _allowed.Contains(capability);
}

public sealed class CapabilityDeniedException(string capability, VerbDescriptor verb)
    : InvalidOperationException($"Capability '{capability}' required by '{verb.Text}' is not allowed.")
{
    public string Capability { get; } = capability;
    public VerbDescriptor Verb { get; } = verb;
}
