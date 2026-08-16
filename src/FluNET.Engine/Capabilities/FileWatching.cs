using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace FluNET.Capabilities;

public enum FluNetFileChangeKind
{
    Created,
    Changed,
    Deleted,
    Renamed
}

public sealed record FluNetFileChange(
    FluNetFileChangeKind Kind,
    string Path,
    string? OldPath = null,
    DateTimeOffset? Timestamp = null,
    bool? IsDirectory = null,
    long? Length = null);

public sealed record FluNetFileWatchOptions(
    string Filter = "*",
    bool Recursive = false,
    TimeSpan? Debounce = null)
{
    public void Validate()
    {
        if (Debounce is { } debounce && debounce < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(Debounce), "Debounce cannot be negative.");
    }
}

public interface IFluNetFileWatcher
{
    IAsyncEnumerable<FluNetFileChange> WatchAsync(
        string directory,
        string filter = "*",
        bool recursive = false,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<FluNetFileChange> WatchAsync(
        string directory,
        FluNetFileWatchOptions options,
        CancellationToken cancellationToken = default) =>
        WatchAsync(
            directory,
            options?.Filter ?? "*",
            options?.Recursive ?? false,
            cancellationToken);
}

public sealed class PhysicalFluNetFileWatcher(IExecutionPolicy policy) : IFluNetFileWatcher
{
    public IAsyncEnumerable<FluNetFileChange> WatchAsync(
        string directory,
        string filter = "*",
        bool recursive = false,
        CancellationToken cancellationToken = default)
        => WatchCoreAsync(
            directory,
            new FluNetFileWatchOptions(filter, recursive),
            cancellationToken);

    public IAsyncEnumerable<FluNetFileChange> WatchAsync(
        string directory,
        FluNetFileWatchOptions options,
        CancellationToken cancellationToken = default) =>
        WatchCoreAsync(directory, options, cancellationToken);

    private async IAsyncEnumerable<FluNetFileChange> WatchCoreAsync(
        string directory,
        FluNetFileWatchOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        string root = Path.GetFullPath(directory);
        policy.EnsureFileAccess(root);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);

        Channel<FluNetFileChange> changes = Channel.CreateUnbounded<FluNetFileChange>();
        using FileSystemWatcher watcher = new(root, string.IsNullOrWhiteSpace(options.Filter) ? "*" : options.Filter)
        {
            IncludeSubdirectories = options.Recursive,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        void Publish(FluNetFileChangeKind kind, string path, string? oldPath = null)
        {
            bool isDirectory = Directory.Exists(path);
            long? length = isDirectory || !File.Exists(path) ? null : new FileInfo(path).Length;
            changes.Writer.TryWrite(new(kind, path, oldPath, DateTimeOffset.UtcNow, isDirectory, length));
        }

        watcher.Created += (_, args) => Publish(FluNetFileChangeKind.Created, args.FullPath);
        watcher.Changed += (_, args) => Publish(FluNetFileChangeKind.Changed, args.FullPath);
        watcher.Deleted += (_, args) => Publish(FluNetFileChangeKind.Deleted, args.FullPath);
        watcher.Renamed += (_, args) => Publish(FluNetFileChangeKind.Renamed, args.FullPath, args.OldFullPath);
        watcher.Error += (_, args) => changes.Writer.TryComplete(args.GetException());
        using CancellationTokenRegistration registration = cancellationToken.Register(() => changes.Writer.TryComplete());

        Dictionary<string, DateTimeOffset> recent = new(StringComparer.OrdinalIgnoreCase);
        await foreach (FluNetFileChange change in changes.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            if (options.Debounce is not { } debounce || debounce == TimeSpan.Zero)
            {
                yield return change;
                continue;
            }

            string key = $"{change.Kind}:{change.Path}";
            DateTimeOffset timestamp = change.Timestamp ?? DateTimeOffset.UtcNow;
            if (recent.TryGetValue(key, out DateTimeOffset previous) && timestamp - previous < debounce)
                continue;
            recent[key] = timestamp;
            yield return change;
        }
    }
}

public sealed class FileWatchCapabilityProvider : ICapabilityProvider
{
    public CapabilityDescriptor Descriptor { get; } = new(
        "filesystem.watch",
        "1.0",
        [FluNetPlatform.Any],
        ["filesystem.read"]);

    public bool IsAvailable => true;
}
