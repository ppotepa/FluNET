namespace FluNET.Capabilities;

public interface IFluNetFileTrash
{
    ValueTask<FileInfo> MoveToTrashAsync(string path, CancellationToken cancellationToken = default);
}

public interface IFluNetDirectoryTrash
{
    ValueTask<DirectoryInfo> MoveDirectoryToTrashAsync(string path, CancellationToken cancellationToken = default);
}

public interface IFluNetFileRestore
{
    ValueTask<FileInfo> RestoreFileAsync(string source, string destination, CancellationToken cancellationToken = default);
}

public interface IFluNetDirectoryRestore
{
    ValueTask<DirectoryInfo> RestoreDirectoryAsync(string source, string destination, CancellationToken cancellationToken = default);
}

public sealed class PortableFluNetFileTrash(IExecutionPolicy policy) :
    IFluNetFileTrash,
    IFluNetDirectoryTrash,
    IFluNetFileRestore,
    IFluNetDirectoryRestore
{
    public ValueTask<FileInfo> MoveToTrashAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string source = Path.GetFullPath(path);
        policy.EnsureFileAccess(source);
        string parent = Path.GetDirectoryName(source) ?? Directory.GetCurrentDirectory();
        string trash = Path.Combine(parent, ".flunet-trash");
        policy.EnsureFileAccess(trash);
        Directory.CreateDirectory(trash);
        string destination = Path.Combine(trash, $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}-{Path.GetFileName(source)}");
        File.Move(source, destination);
        return ValueTask.FromResult(new FileInfo(destination));
    }

    public ValueTask<DirectoryInfo> MoveDirectoryToTrashAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string source = Path.GetFullPath(path);
        policy.EnsureFileAccess(source);
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException(source);
        string parent = Path.GetDirectoryName(source) ?? Directory.GetCurrentDirectory();
        string trash = Path.Combine(parent, ".flunet-trash");
        policy.EnsureFileAccess(trash);
        Directory.CreateDirectory(trash);
        string destination = Path.Combine(trash, $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}-{Path.GetFileName(source)}");
        Directory.Move(source, destination);
        return ValueTask.FromResult(new DirectoryInfo(destination));
    }

    public ValueTask<FileInfo> RestoreFileAsync(string source, string destination, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fullSource = ValidateTrashSource(source, isDirectory: false);
        string fullDestination = Path.GetFullPath(destination);
        policy.EnsureFileAccess(fullDestination);
        Directory.CreateDirectory(Path.GetDirectoryName(fullDestination) ?? Directory.GetCurrentDirectory());
        File.Move(fullSource, fullDestination, overwrite: false);
        return ValueTask.FromResult(new FileInfo(fullDestination));
    }

    public ValueTask<DirectoryInfo> RestoreDirectoryAsync(string source, string destination, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fullSource = ValidateTrashSource(source, isDirectory: true);
        string fullDestination = Path.GetFullPath(destination);
        policy.EnsureFileAccess(fullDestination);
        Directory.Move(fullSource, fullDestination);
        return ValueTask.FromResult(new DirectoryInfo(fullDestination));
    }

    private string ValidateTrashSource(string source, bool isDirectory)
    {
        string fullSource = Path.GetFullPath(source);
        policy.EnsureFileAccess(fullSource);
        string? parent = Path.GetDirectoryName(fullSource);
        if (parent is null || !Path.GetFileName(parent).Equals(".flunet-trash", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("RESTORE only accepts items directly inside a .flunet-trash directory.");
        bool exists = isDirectory ? Directory.Exists(fullSource) : File.Exists(fullSource);
        if (!exists) throw new FileNotFoundException("Trash item was not found.", fullSource);
        return fullSource;
    }
}

public sealed class FileTrashCapabilityProvider : ICapabilityProvider
{
    public CapabilityDescriptor Descriptor { get; } = new(
        "filesystem.trash",
        "1.0",
        [FluNetPlatform.Any],
        ["filesystem.read", "filesystem.write"]);

    public bool IsAvailable => true;
}
