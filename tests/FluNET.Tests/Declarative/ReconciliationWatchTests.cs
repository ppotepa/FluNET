using FluNET.Capabilities;
using FluNET.Context;
using FluNET.Declarative.Reconciliation;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;

namespace FluNET.Tests.Declarative;

[TestFixture]
public sealed class ReconciliationWatchTests
{
    [Test]
    public void WatchCompilerEmbedsSyncDefinition()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        const string source = """
WATCH users.changed
    WHEN updated
        SYNC target.json WITH desired.json BY id
""";
        ReconciliationWatchCompilationResult result = context.CompileReconciliationWatches(source);
        Assert.That(result.IsValid, Is.True, string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Multiple(() =>
        {
            Assert.That(result.Watches, Has.Count.EqualTo(1));
            Assert.That(result.Watches[0].Trigger.Event, Is.EqualTo("updated"));
            Assert.That(result.Watches[0].SyncDefinitions.Single().Goal.KeyField, Is.EqualTo("id"));
        });
    }

    [Test]
    public async Task SignalRunsTheEmbeddedSyncDefinition()
    {
        MemoryFiles files = new(new Dictionary<string, string>
        {
            ["target.json"] = "[{\"id\":1,\"name\":\"old\"}]",
            ["desired.json"] = "[{\"id\":1,\"name\":\"new\"}]"
        });
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(services =>
            services.AddSingleton<IFluNetFileSystem>(files));
        ReconciliationWatchCompilationResult compilation = context.CompileReconciliationWatches("""
WATCH users.changed
    WHEN updated
        SYNC target.json WITH desired.json BY id
""");
        ReconciliationWatchScheduler scheduler = context.GetReconciliationWatchScheduler();
        foreach (ReconciliationWatchDefinition watch in compilation.Watches) scheduler.Register(watch);

        IReadOnlyList<ReconciliationWatchRunResult> runs = await scheduler.PublishSignalAsync("users.changed", "updated");

        Assert.Multiple(() =>
        {
            Assert.That(runs, Has.Count.EqualTo(1));
            Assert.That(runs[0].IsSuccess, Is.True);
            Assert.That(runs[0].Reconciliations.Single().Applied, Is.True);
        });
        using JsonDocument saved = JsonDocument.Parse(files.Get("target.json"));
        Assert.That(saved.RootElement[0].GetProperty("name").GetString(), Is.EqualTo("new"));
    }

    private sealed class MemoryFiles : IFluNetFileSystem
    {
        private readonly Dictionary<string, string> _values;
        public MemoryFiles(IReadOnlyDictionary<string, string> values) =>
            _values = values.ToDictionary(item => Path.GetFullPath(item.Key), item => item.Value, PathComparer);
        public string Get(string path) => _values[Path.GetFullPath(path)];
        public Task<string[]> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(Read(path).Split('\n'));
        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(Read(path));
        public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
        { _values[Path.GetFullPath(path)] = content; return Task.CompletedTask; }
        public Task WriteAllBytesAsync(string path, byte[] content, CancellationToken cancellationToken = default)
        { _values[Path.GetFullPath(path)] = Encoding.UTF8.GetString(content); return Task.CompletedTask; }
        public ValueTask<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_values.ContainsKey(Path.GetFullPath(path)));
        public ValueTask DeleteFileAsync(string path, CancellationToken cancellationToken = default)
        { _values.Remove(Path.GetFullPath(path)); return ValueTask.CompletedTask; }
        private string Read(string path) => _values.TryGetValue(Path.GetFullPath(path), out string? value)
            ? value
            : throw new FileNotFoundException(path);
        private static StringComparer PathComparer => OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    }
}
