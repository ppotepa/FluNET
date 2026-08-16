using FluNET.Capabilities;
using FluNET.Context;
using NUnit.Framework;
using System.Security.Cryptography;
using System.Text.Json;

namespace FluNET.Tests.Capabilities;

[TestFixture]
public sealed class FileEnumerationTests
{
    [Test]
    public async Task FileWatcherPublishesPortableChanges()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-watch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "created.txt");
        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(5));
        try
        {
            IFluNetFileWatcher watcher = new PhysicalFluNetFileWatcher(new AllowAllExecutionPolicy());
            IAsyncEnumerator<FluNetFileChange> events = watcher.WatchAsync(root, "*.txt", cancellationToken: cancellation.Token).GetAsyncEnumerator(cancellation.Token);
            ValueTask<bool> pending = events.MoveNextAsync();
            await File.WriteAllTextAsync(path, "event");

            Assert.That(await pending.AsTask().WaitAsync(TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(events.Current.Kind, Is.EqualTo(FluNetFileChangeKind.Created));
            await events.DisposeAsync();
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task FileWatcherOptionsExposeMetadataAndDebounce()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-watch-options-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "created.txt");
        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(5));
        try
        {
            IFluNetFileWatcher watcher = new PhysicalFluNetFileWatcher(new AllowAllExecutionPolicy());
            IAsyncEnumerator<FluNetFileChange> events = watcher.WatchAsync(
                root,
                new FluNetFileWatchOptions("*.txt", Recursive: false, Debounce: TimeSpan.FromMilliseconds(50)),
                cancellation.Token).GetAsyncEnumerator(cancellation.Token);
            ValueTask<bool> pending = events.MoveNextAsync();
            await File.WriteAllTextAsync(path, "event");

            Assert.That(await pending.AsTask().WaitAsync(TimeSpan.FromSeconds(5)), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(events.Current.Kind, Is.EqualTo(FluNetFileChangeKind.Created));
                Assert.That(events.Current.IsDirectory, Is.False);
                Assert.That(events.Current.Length is null or >= 0, Is.True);
                Assert.That(events.Current.Timestamp, Is.Not.Null);
            });
            await events.DisposeAsync();
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void FileWatchOptionsRejectNegativeDebounce()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FluNetFileWatchOptions(Debounce: TimeSpan.FromMilliseconds(-1)).Validate());
    }

    [Test]
    public async Task TrashMovesFilesToRecoverablePortableLocation()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-trash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string source = Path.Combine(root, "old.txt");
        try
        {
            await File.WriteAllTextAsync(source, "recover me");
            using FluNETContext context = FluNETContext.Create();
            SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(
                $"TRASH \"{source}\" AS removed");

            Assert.That(execution.IsSuccess, Is.True,
                execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.SurfaceParse.Diagnostics.Select(item => item.Message)
                    .Concat(execution.Compilation.Lowering.Diagnostics.Select(item => item.Message))
                    .Concat(execution.Compilation.Diagnostics.Select(item => item.Message))));
            Assert.That(File.Exists(source), Is.False);
            Assert.That(Directory.EnumerateFiles(Path.Combine(root, ".flunet-trash")), Is.Not.Empty);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task CopyExecutesThroughThePortableFileCapability()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-copy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string source = Path.Combine(root, "source.txt");
        string destination = Path.Combine(root, "backup", "source.txt");
        try
        {
            await File.WriteAllTextAsync(source, "copy me");
            using FluNETContext context = FluNETContext.Create();
            SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(
                $"COPY \"{source}\" TO \"{destination}\" AS backup");

            Assert.That(execution.IsSuccess, Is.True, execution.Error?.ToString());
            Assert.That(File.Exists(destination), Is.True);
            Assert.That(await File.ReadAllTextAsync(destination), Is.EqualTo("copy me"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task FindExecutesRecursivelyThroughTheSurfaceExecutor()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-find-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "nested"));
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "first.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(root, "nested", "second.json"), "{}");
            using FluNETContext context = FluNETContext.Create();

            SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(
                $"FIND \"{Path.Combine(root, "*.json")}\" AS files");

            Assert.That(execution.IsSuccess, Is.True, execution.Error?.ToString());
            Assert.That(execution.Result, Is.TypeOf<JsonElement[]>());
            Assert.That((JsonElement[])execution.Result!, Has.Length.EqualTo(2));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task FindAcceptsADirectoryAsARecursiveRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-find-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "nested"));
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "top.txt"), "top");
            await File.WriteAllTextAsync(Path.Combine(root, "nested", "deep.txt"), "deep");
            using FluNETContext context = FluNETContext.Create();

            SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(
                $"FIND \"{root}\" AS files");

            Assert.That(execution.IsSuccess, Is.True,
                execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Lowering.Diagnostics.Select(item => item.Message)
                    .Concat(execution.Compilation.Diagnostics.Select(item => item.Message))));
            JsonElement[] files = (JsonElement[])execution.Result!;
            Assert.That(files.Select(file => file.GetProperty("name").GetString()),
                Is.EquivalentTo(new[] { "top.txt", "deep.txt" }));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task FindCanFilterDirectoryResultsByMetadata()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-find-filter-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "keep.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(root, "skip.txt"), "text");
            using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();

            SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync($"""
FIND "{root}" AS files
WHERE extension == ".json"
""");

            Assert.That(execution.IsSuccess, Is.True,
                execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.SurfaceParse.Diagnostics.Select(item => item.Message)
                    .Concat(execution.Compilation.Lowering.Diagnostics.Select(item => item.Message))
                    .Concat(execution.Compilation.Diagnostics.Select(item => item.Message))));
            JsonElement[] files = (JsonElement[])execution.Result!;
            Assert.That(files, Has.Length.EqualTo(1));
            Assert.That(files[0].GetProperty("name").GetString(), Is.EqualTo("keep.json"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task PhysicalHasherReturnsStableSha256()
    {
        string path = Path.Combine(Path.GetTempPath(), "flunet-file-hash-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            byte[] content = "hello FluNET"u8.ToArray();
            await File.WriteAllBytesAsync(path, content);
            PhysicalFluNetFileHasher hasher = new(new AllowAllExecutionPolicy());

            string actual = await hasher.ComputeSha256Async(path);

            Assert.That(actual, Is.EqualTo(Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant()));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Test]
    public async Task PhysicalEnumeratorSupportsRecursivePatterns()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-file-enumerator-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "nested"));
        string first = Path.Combine(root, "first.json");
        string second = Path.Combine(root, "nested", "second.json");

        try
        {
            await File.WriteAllTextAsync(first, "{}");
            await File.WriteAllTextAsync(second, "{}");
            PhysicalFluNetFileEnumerator enumerator = new(new AllowAllExecutionPolicy());

            IReadOnlyList<string> files = await enumerator.EnumerateFilesAsync(
                Path.Combine(root, "*.json"),
                SearchOption.AllDirectories);

            Assert.That(files, Is.EqualTo(new[] { first, second }.Order(StringComparer.OrdinalIgnoreCase)));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task PhysicalEnumeratorStopsAtProviderLimit()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-file-enumerator-limit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            for (int index = 0; index < 5; index++)
                await File.WriteAllTextAsync(Path.Combine(root, $"{index}.txt"), string.Empty);

            PhysicalFluNetFileEnumerator enumerator = new(new AllowAllExecutionPolicy());
            IReadOnlyList<string> files = await enumerator.EnumerateFilesAsync(
                Path.Combine(root, "*.txt"), SearchOption.TopDirectoryOnly, 2);

            Assert.That(files, Has.Count.EqualTo(2));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
