using FluNET.Capabilities;

namespace FluNET.Automation;

/// <summary>
/// Bridges the portable file event stream to the existing host-driven
/// WATCH/WHEN scheduler. It owns no background thread; the host controls the
/// lifetime through the returned async operation and cancellation token.
/// </summary>
public sealed class FileWatchAutomationBridge(
    IFluNetFileWatcher watcher,
    AutomationScheduler scheduler,
    IAutomationSignalStore? signalStore = null)
{
    public async ValueTask RunAsync(
        string directory,
        string resource,
        string filter = "*",
        bool recursive = false,
        CancellationToken cancellationToken = default,
        Func<FluNetFileChange, IReadOnlyList<AutomationRunResult>, ValueTask>? onSignal = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        await foreach (FluNetFileChange change in watcher.WatchAsync(
            directory, filter, recursive, cancellationToken).ConfigureAwait(false))
        {
            AutomationSignal signal = AutomationSignal.FromFileChange(resource, change);
            if (signalStore is not null)
                await signalStore.AppendAsync(new AutomationSignalEnvelope(change.Timestamp ?? DateTimeOffset.UtcNow, signal), cancellationToken).ConfigureAwait(false);
            IReadOnlyList<AutomationRunResult> runs = await scheduler.PublishSignalAsync(
                signal,
                cancellationToken).ConfigureAwait(false);
            if (onSignal is not null)
                await onSignal(change, runs).ConfigureAwait(false);
        }
    }

    public ValueTask RunAsync(
        string directory,
        string resource,
        FluNetFileWatchOptions options,
        CancellationToken cancellationToken = default,
        Func<FluNetFileChange, IReadOnlyList<AutomationRunResult>, ValueTask>? onSignal = null) =>
        RunWithOptionsAsync(directory, resource, options, cancellationToken, onSignal);

    private async ValueTask RunWithOptionsAsync(
        string directory,
        string resource,
        FluNetFileWatchOptions options,
        CancellationToken cancellationToken,
        Func<FluNetFileChange, IReadOnlyList<AutomationRunResult>, ValueTask>? onSignal)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        await foreach (FluNetFileChange change in watcher.WatchAsync(
            directory, options, cancellationToken).ConfigureAwait(false))
        {
            AutomationSignal signal = AutomationSignal.FromFileChange(resource, change);
            if (signalStore is not null)
                await signalStore.AppendAsync(new AutomationSignalEnvelope(change.Timestamp ?? DateTimeOffset.UtcNow, signal), cancellationToken).ConfigureAwait(false);
            IReadOnlyList<AutomationRunResult> runs = await scheduler.PublishSignalAsync(
                signal,
                cancellationToken).ConfigureAwait(false);
            if (onSignal is not null)
                await onSignal(change, runs).ConfigureAwait(false);
        }
    }
}
