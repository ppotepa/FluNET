namespace FluNET.Capabilities;

public interface IEnvironmentReader
{
    string? Get(string name);
}

public sealed class ProcessEnvironmentReader : IEnvironmentReader
{
    public string? Get(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Environment.GetEnvironmentVariable(name);
    }
}

public interface IEnvironmentWriter
{
    void Set(string name, string value);
}

public interface IEnvironmentWritePolicy
{
    void EnsureWrite(string name, string value);
}

public sealed class AllowAllEnvironmentWritePolicy : IEnvironmentWritePolicy
{
    public void EnsureWrite(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
    }
}

public sealed class DenyAllEnvironmentWritePolicy : IEnvironmentWritePolicy
{
    public void EnsureWrite(string name, string value) =>
        throw new CapabilityDeniedException("Environment mutation is not allowed by the host policy.");
}

public sealed class ProcessEnvironmentWriter(IEnvironmentWritePolicy policy) : IEnvironmentWriter
{
    public void Set(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        policy.EnsureWrite(name, value);
        Environment.SetEnvironmentVariable(name, value);
    }
}

public sealed class DenyEnvironmentWriter : IEnvironmentWriter
{
    public void Set(string name, string value) =>
        throw new CapabilityDeniedException("Environment mutation is not available in this host.");
}

public sealed class EnvironmentCapabilityProvider(IEnvironmentReader reader) : ICapabilityProvider
{
    public CapabilityDescriptor Descriptor { get; } = new(
        "system.environment",
        "1.0",
        [FluNetPlatform.Any],
        ["system.environment.read"]);

    public bool IsAvailable => reader is not null;
}

public sealed class EnvironmentWriteCapabilityProvider(IEnvironmentWriter writer) : ICapabilityProvider
{
    public CapabilityDescriptor Descriptor { get; } = new(
        "system.environment.write",
        "1.0",
        [FluNetPlatform.Any],
        ["system.environment.write"]);

    public bool IsAvailable => writer is not DenyEnvironmentWriter;
}
