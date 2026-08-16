namespace FluNET.Capabilities;

public interface IFluNetDirectoryOperations
{
    ValueTask<FluNetPathInfo> StatAsync(
        string path,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<FluNetDirectoryEntry>> ListAsync(
        string path,
        bool recursive = false,
        CancellationToken cancellationToken = default);

    ValueTask<DirectoryInfo> CreateAsync(
        string path,
        CancellationToken cancellationToken = default);
}

public sealed record FluNetDirectoryEntry(
    string Path,
    string Name,
    bool IsDirectory,
    long Length,
    DateTimeOffset ModifiedUtc,
    DateTimeOffset? CreatedUtc = null,
    bool IsHidden = false);

public sealed record FluNetPathInfo(
    string Path,
    string Name,
    bool Exists,
    bool IsDirectory,
    long Length,
    DateTimeOffset? CreatedUtc,
    DateTimeOffset? ModifiedUtc);

public sealed class PhysicalFluNetDirectoryOperations(IExecutionPolicy policy)
    : IFluNetDirectoryOperations
{
    public ValueTask<FluNetPathInfo> StatAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fullPath = Path.GetFullPath(path);
        policy.EnsureFileAccess(fullPath);
        if (Directory.Exists(fullPath))
        {
            DirectoryInfo directory = new(fullPath);
            return ValueTask.FromResult(new FluNetPathInfo(
                directory.FullName, directory.Name, true, true, 0,
                directory.CreationTimeUtc, directory.LastWriteTimeUtc));
        }
        if (File.Exists(fullPath))
        {
            FileInfo file = new(fullPath);
            return ValueTask.FromResult(new FluNetPathInfo(
                file.FullName, file.Name, true, false, file.Length,
                file.CreationTimeUtc, file.LastWriteTimeUtc));
        }
        return ValueTask.FromResult(new FluNetPathInfo(
            fullPath, Path.GetFileName(fullPath), false, false, 0, null, null));
    }

    public ValueTask<IReadOnlyList<FluNetDirectoryEntry>> ListAsync(
        string path,
        bool recursive = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fullPath = Path.GetFullPath(path);
        policy.EnsureFileAccess(fullPath);
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException(fullPath);

        List<FluNetDirectoryEntry> entries = [];
        EnumerationOptions options = new()
        {
            RecurseSubdirectories = recursive,
            IgnoreInaccessible = false,
            ReturnSpecialDirectories = false
        };
        foreach (string entryPath in Directory.EnumerateFileSystemEntries(fullPath, "*", options).Order(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            policy.EnsureFileAccess(entryPath);
            if (Directory.Exists(entryPath))
            {
                DirectoryInfo directory = new(entryPath);
                entries.Add(new(
                    directory.FullName,
                    directory.Name,
                    true,
                    0,
                    directory.LastWriteTimeUtc,
                    directory.CreationTimeUtc,
                    IsHidden(directory)));
            }
            else
            {
                FileInfo file = new(entryPath);
                entries.Add(new(
                    file.FullName,
                    file.Name,
                    false,
                    file.Length,
                    file.LastWriteTimeUtc,
                    file.CreationTimeUtc,
                    IsHidden(file)));
            }
        }

        return ValueTask.FromResult<IReadOnlyList<FluNetDirectoryEntry>>(entries);
    }

    public ValueTask<DirectoryInfo> CreateAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fullPath = Path.GetFullPath(path);
        policy.EnsureFileAccess(fullPath);
        return ValueTask.FromResult(Directory.CreateDirectory(fullPath));
    }

    private static bool IsHidden(FileSystemInfo info) =>
        info.Name.StartsWith(".", StringComparison.Ordinal) ||
        info.Attributes.HasFlag(FileAttributes.Hidden);
}

public sealed class DirectoryOperationsCapabilityProvider : ICapabilityProvider
{
    public CapabilityDescriptor Descriptor { get; } = new(
        "filesystem.directory",
        "1.0",
        [FluNetPlatform.Any],
        ["filesystem.read", "filesystem.write"]);

    public bool IsAvailable => true;
}
