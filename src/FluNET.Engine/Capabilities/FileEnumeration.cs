namespace FluNET.Capabilities;

/// <summary>Capability boundary for deterministic file-pattern enumeration.</summary>
public interface IFluNetFileEnumerator
{
    ValueTask<IReadOnlyList<string>> EnumerateFilesAsync(
        string pattern,
        CancellationToken cancellationToken = default);
}

public sealed class PhysicalFluNetFileEnumerator(IExecutionPolicy policy) : IFluNetFileEnumerator
{
    public ValueTask<IReadOnlyList<string>> EnumerateFilesAsync(
        string pattern,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        string fullPattern = Path.GetFullPath(pattern);
        string directory = Path.GetDirectoryName(fullPattern) ?? Directory.GetCurrentDirectory();
        string filePattern = Path.GetFileName(fullPattern);
        policy.EnsureFileAccess(directory);
        string[] files = Directory
            .EnumerateFiles(directory, filePattern, SearchOption.TopDirectoryOnly)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (string file in files)
        {
            policy.EnsureFileAccess(file);
        }
        return ValueTask.FromResult<IReadOnlyList<string>>(files);
    }
}
