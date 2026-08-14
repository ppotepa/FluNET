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
