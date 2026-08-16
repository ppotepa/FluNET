namespace FluNET.Capabilities;

/// <summary>
/// Keeps a metadata index fresh from the portable file-change stream. The host
/// owns the lifetime by controlling the returned operation and cancellation.
/// A full rebuild is intentional: it keeps rename/delete semantics correct for
/// every index provider and can later be replaced by an incremental provider.
/// </summary>
public sealed class FileMetadataIndexWatcher(
    IFluNetFileWatcher watcher,
    IFluNetFileMetadataIndex index)
{
    public async ValueTask RunAsync(
        string root,
        bool recursive = true,
        FluNetFileWatchOptions? options = null,
        CancellationToken cancellationToken = default,
        Func<IReadOnlyList<FluNetFileIndexEntry>, FluNetFileChange, ValueTask>? onRebuilt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        FluNetFileWatchOptions effectiveOptions = options ?? new FluNetFileWatchOptions("*", recursive);
        await index.RebuildAsync(root, recursive, cancellationToken).ConfigureAwait(false);
        await foreach (FluNetFileChange change in watcher.WatchAsync(root, effectiveOptions, cancellationToken).ConfigureAwait(false))
        {
            IReadOnlyList<FluNetFileIndexEntry> snapshot = await index.ApplyChangeAsync(root, change, recursive, cancellationToken).ConfigureAwait(false);
            if (onRebuilt is not null)
                await onRebuilt(snapshot, change).ConfigureAwait(false);
        }
    }
}
