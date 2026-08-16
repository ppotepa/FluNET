namespace FluNET.Capabilities;

public interface IFluNetFileOperations
{
    ValueTask<FileInfo> CopyAsync(string source, string destination, CancellationToken cancellationToken = default);
    ValueTask<FileInfo> MoveAsync(string source, string destination, CancellationToken cancellationToken = default);
    ValueTask<DirectoryInfo> CopyDirectoryAsync(string source, string destination, CancellationToken cancellationToken = default);
    ValueTask<DirectoryInfo> MoveDirectoryAsync(string source, string destination, CancellationToken cancellationToken = default);
}

public sealed class PhysicalFluNetFileOperations(IExecutionPolicy policy) : IFluNetFileOperations
{
    public ValueTask<FileInfo> CopyAsync(
        string source,
        string destination,
        CancellationToken cancellationToken = default) =>
        TransferAsync(source, destination, move: false, cancellationToken);

    public ValueTask<FileInfo> MoveAsync(
        string source,
        string destination,
        CancellationToken cancellationToken = default) =>
        TransferAsync(source, destination, move: true, cancellationToken);

    public ValueTask<DirectoryInfo> CopyDirectoryAsync(string source, string destination, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fullSource = Path.GetFullPath(source);
        string fullDestination = Path.GetFullPath(destination);
        EnsureDirectoryTransfer(fullSource, fullDestination);
        Directory.CreateDirectory(fullDestination);
        foreach (string directory in Directory.EnumerateDirectories(fullSource, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string target = Path.Combine(fullDestination, Path.GetRelativePath(fullSource, directory));
            policy.EnsureFileAccess(directory);
            policy.EnsureFileAccess(target);
            Directory.CreateDirectory(target);
        }
        foreach (string file in Directory.EnumerateFiles(fullSource, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string target = Path.Combine(fullDestination, Path.GetRelativePath(fullSource, file));
            policy.EnsureFileAccess(file);
            policy.EnsureFileAccess(target);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
        return ValueTask.FromResult(new DirectoryInfo(fullDestination));
    }

    public ValueTask<DirectoryInfo> MoveDirectoryAsync(string source, string destination, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fullSource = Path.GetFullPath(source);
        string fullDestination = Path.GetFullPath(destination);
        EnsureDirectoryTransfer(fullSource, fullDestination);
        Directory.Move(fullSource, fullDestination);
        return ValueTask.FromResult(new DirectoryInfo(fullDestination));
    }

    private ValueTask<FileInfo> TransferAsync(
        string source,
        string destination,
        bool move,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fullSource = Path.GetFullPath(source);
        string fullDestination = Path.GetFullPath(destination);
        policy.EnsureFileAccess(fullSource);
        policy.EnsureFileAccess(fullDestination);
        Directory.CreateDirectory(Path.GetDirectoryName(fullDestination) ?? Directory.GetCurrentDirectory());
        if (move) File.Move(fullSource, fullDestination, overwrite: true);
        else File.Copy(fullSource, fullDestination, overwrite: true);
        return ValueTask.FromResult(new FileInfo(fullDestination));
    }

    private void EnsureDirectoryTransfer(string source, string destination)
    {
        policy.EnsureFileAccess(source);
        policy.EnsureFileAccess(destination);
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException(source);
        string prefix = source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (destination.Equals(source, StringComparison.OrdinalIgnoreCase) ||
            destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new IOException("A directory cannot be copied or moved into itself.");
    }
}

public sealed class FileOperationsCapabilityProvider : ICapabilityProvider
{
    public CapabilityDescriptor Descriptor { get; } = new(
        "filesystem.write",
        "1.0",
        [FluNetPlatform.Any],
        ["filesystem.read", "filesystem.write"]);

    public bool IsAvailable => true;
}
