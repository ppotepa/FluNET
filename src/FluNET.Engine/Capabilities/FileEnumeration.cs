namespace FluNET.Capabilities;

/// <summary>Capability boundary for deterministic file-pattern enumeration.</summary>
public interface IFluNetFileEnumerator
{
    ValueTask<IReadOnlyList<string>> EnumerateFilesAsync(
        string pattern,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<string>> EnumerateFilesAsync(
        string pattern,
        SearchOption searchOption,
        CancellationToken cancellationToken = default)
    {
        if (searchOption == SearchOption.TopDirectoryOnly)
            return EnumerateFilesAsync(pattern, cancellationToken);
        throw new NotSupportedException("This file provider does not support recursive enumeration.");
    }

    ValueTask<IReadOnlyList<string>> EnumerateFilesAsync(
        string pattern,
        SearchOption searchOption,
        int maxFiles,
        CancellationToken cancellationToken = default)
    {
        if (maxFiles <= 0) return EnumerateFilesAsync(pattern, searchOption, cancellationToken);
        return new ValueTask<IReadOnlyList<string>>(
            EnumerateFilesAsync(pattern, searchOption, cancellationToken)
                .AsTask()
                .ContinueWith(task => (IReadOnlyList<string>)task.Result.Take(maxFiles).ToArray(), cancellationToken));
    }
}

public sealed class PhysicalFluNetFileEnumerator(IExecutionPolicy policy) : IFluNetFileEnumerator
{
    public ValueTask<IReadOnlyList<string>> EnumerateFilesAsync(
        string pattern,
        CancellationToken cancellationToken = default)
        => EnumerateFilesAsync(pattern, SearchOption.TopDirectoryOnly, cancellationToken);

    public ValueTask<IReadOnlyList<string>> EnumerateFilesAsync(
        string pattern,
        SearchOption searchOption,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        string fullPattern = Path.GetFullPath(pattern);
        string directory = Path.GetDirectoryName(fullPattern) ?? Directory.GetCurrentDirectory();
        string filePattern = Path.GetFileName(fullPattern);
        policy.EnsureFileAccess(directory);
        string[] files = Directory
        .EnumerateFiles(directory, filePattern, searchOption)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (string file in files)
        {
            policy.EnsureFileAccess(file);
        }
        return ValueTask.FromResult<IReadOnlyList<string>>(files);
    }

    public ValueTask<IReadOnlyList<string>> EnumerateFilesAsync(
        string pattern,
        SearchOption searchOption,
        int maxFiles,
        CancellationToken cancellationToken = default)
    {
        if (maxFiles <= 0) return EnumerateFilesAsync(pattern, searchOption, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        string fullPattern = Path.GetFullPath(pattern);
        string directory = Path.GetDirectoryName(fullPattern) ?? Directory.GetCurrentDirectory();
        string filePattern = Path.GetFileName(fullPattern);
        policy.EnsureFileAccess(directory);
        List<string> files = [];
        foreach (string file in Directory.EnumerateFiles(directory, filePattern, searchOption))
        {
            policy.EnsureFileAccess(file);
            files.Add(file);
            if (files.Count >= maxFiles) break;
        }
        return ValueTask.FromResult<IReadOnlyList<string>>(files);
    }
}

public sealed class FileScanCapabilityProvider : ICapabilityProvider
{
    public CapabilityDescriptor Descriptor { get; } = new(
        "filesystem.scan",
        "1.0",
        [FluNetPlatform.Any],
        ["filesystem.read"]);

    public bool IsAvailable => true;
}
