using FluNET.Capabilities;
using NUnit.Framework;

namespace FluNET.Tests.Capabilities;

public sealed class FileMetadataIndexTests
{
    [Test]
    public async Task WatchBridge_RebuildsIndexAfterChanges()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-index-watch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string first = Path.Combine(root, "first.txt");
        string second = Path.Combine(root, "second.txt");
        await File.WriteAllTextAsync(first, "one");
        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(5));
        try
        {
            PhysicalFluNetFileMetadataIndex index = new(new AllowAllExecutionPolicy());
            TaskCompletionSource<IReadOnlyList<FluNetFileIndexEntry>> rebuilt = new(TaskCreationOptions.RunContinuationsAsynchronously);
            FileMetadataIndexWatcher bridge = new(
                new PhysicalFluNetFileWatcher(new AllowAllExecutionPolicy()), index);
            Task run = bridge.RunAsync(root, recursive: false, new FluNetFileWatchOptions("*.txt"), cancellation.Token,
                (snapshot, _) =>
                {
                    if (snapshot.Any(entry => entry.Name == "second.txt")) rebuilt.TrySetResult(snapshot);
                    return ValueTask.CompletedTask;
                }).AsTask();
            await Task.Delay(100, cancellation.Token);
            await File.WriteAllTextAsync(second, "two", cancellation.Token);
            IReadOnlyList<FluNetFileIndexEntry> snapshot = await rebuilt.Task.WaitAsync(cancellation.Token);
            Assert.That(snapshot.Select(entry => entry.Name), Does.Contain("first.txt"));
            Assert.That(snapshot.Select(entry => entry.Name), Does.Contain("second.txt"));
            cancellation.Cancel();
            try { await run; } catch (OperationCanceledException) { }
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task PhysicalIndexAppliesFileChangesIncrementally()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-index-delta-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string first = Path.Combine(root, "first.txt");
        string second = Path.Combine(root, "second.txt");
        await File.WriteAllTextAsync(first, "one");
        try
        {
            PhysicalFluNetFileMetadataIndex index = new(new AllowAllExecutionPolicy());
            await index.RebuildAsync(root, recursive: false);
            await File.WriteAllTextAsync(second, "two");
            IReadOnlyList<FluNetFileIndexEntry> afterCreate = await index.ApplyChangeAsync(
                root, new FluNetFileChange(FluNetFileChangeKind.Created, second, IsDirectory: false), recursive: false);
            Assert.That(afterCreate.Select(entry => entry.Name), Does.Contain("second.txt"));

            File.Delete(second);
            IReadOnlyList<FluNetFileIndexEntry> afterDelete = await index.ApplyChangeAsync(
                root, new FluNetFileChange(FluNetFileChangeKind.Deleted, second, IsDirectory: false), recursive: false);
            Assert.That(afterDelete.Select(entry => entry.Name), Does.Not.Contain("second.txt"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
