using FluNET.Capabilities;
using FluNET.Context;
using FluNET.Declarative.Reconciliation;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;

namespace FluNET.Tests.Declarative;

[TestFixture]
public sealed class ReconciliationRunnerTests
{
    [Test]
    public async Task SyncAppliesDesiredSnapshotToLocalJsonTarget()
    {
        MemoryFiles files = new(new Dictionary<string, string>
        {
            ["target.json"] = "[{\"id\":1,\"name\":\"old\"},{\"id\":3,\"name\":\"delete\"}]",
            ["desired.json"] = "[{\"id\":1,\"name\":\"new\"},{\"id\":2,\"name\":\"create\"}]"
        });
        using FluNETContext context = Create(files);

        ReconciliationRunResult run = (await context.ExecuteSyncAsync(
            "SYNC target.json WITH desired.json BY id")).Single();

        Assert.Multiple(() =>
        {
            Assert.That(run.IsSuccess, Is.True, run.Error?.Message);
            Assert.That(run.Applied, Is.True);
            Assert.That(run.Diff!.Creates, Is.EqualTo(1));
            Assert.That(run.Diff.Updates, Is.EqualTo(1));
            Assert.That(run.Diff.Deletes, Is.EqualTo(1));
        });
        using JsonDocument saved = JsonDocument.Parse(files.Get("target.json"));
        Assert.That(saved.RootElement.EnumerateArray().Select(item => item.GetProperty("id").GetInt32()),
            Is.EqualTo(new[] { 1, 2 }));
    }

    [Test]
    public async Task EqualStateIsNoOpAndDoesNotWrite()
    {
        const string json = "[{\"id\":1,\"name\":\"same\"}]";
        MemoryFiles files = new(new Dictionary<string, string>
        {
            ["target.json"] = json,
            ["desired.json"] = json
        });
        using FluNETContext context = Create(files);

        ReconciliationRunResult run = (await context.ExecuteSyncAsync(
            "SYNC target.json WITH desired.json BY id")).Single();

        Assert.Multiple(() =>
        {
            Assert.That(run.IsSuccess, Is.True, run.Error?.Message);
            Assert.That(run.Applied, Is.False);
            Assert.That(files.WriteCount, Is.Zero);
        });
    }

    [Test]
    public async Task ThreeWayConflictBlocksMutation()
    {
        MemoryFiles files = new(new Dictionary<string, string>
        {
            ["target.json"] = "[{\"id\":1,\"name\":\"target-change\"}]",
            ["desired.json"] = "[{\"id\":1,\"name\":\"desired-change\"}]"
        });
        using FluNETContext context = Create(files);
        SyncDefinition definition = context.CompileSync(
            "SYNC target.json WITH desired.json BY id").Definitions.Single();
        using JsonDocument baselineJson = JsonDocument.Parse("[{\"id\":1,\"name\":\"base\"}]");
        ObservedStateSnapshot baseline = new(
            new ResourceIdentity("file", "target.json"),
            "id",
            baselineJson.RootElement.EnumerateArray().Select(item => item.Clone()));

        ReconciliationRunResult run = await context.GetReconciliationRunner()
            .RunAsync(definition, baseline);

        Assert.Multiple(() =>
        {
            Assert.That(run.IsSuccess, Is.False);
            Assert.That(run.Error, Is.TypeOf<ReconciliationConflictException>());
            Assert.That(run.Diff!.Conflicts, Is.EqualTo(1));
            Assert.That(run.Applied, Is.False);
            Assert.That(files.WriteCount, Is.Zero);
        });
    }

    private static FluNETContext Create(MemoryFiles files) =>
        SurfaceCompilationExtensions.CreateSurfaceContext(services =>
            services.AddSingleton<IFluNetFileSystem>(files));

    private sealed class MemoryFiles : IFluNetFileSystem
    {
        private readonly Dictionary<string, string> _values;
        public MemoryFiles(IReadOnlyDictionary<string, string> values) =>
            _values = values.ToDictionary(item => Normalize(item.Key), item => item.Value, PathComparer);
        public int WriteCount { get; private set; }
        public string Get(string path) => _values[Normalize(path)];
        public Task<string[]> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(Read(path).Split('\n'));
        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(Read(path));
        public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
        { _values[Normalize(path)] = content; WriteCount++; return Task.CompletedTask; }
        public Task WriteAllBytesAsync(string path, byte[] content, CancellationToken cancellationToken = default)
        { _values[Normalize(path)] = Encoding.UTF8.GetString(content); WriteCount++; return Task.CompletedTask; }
        public ValueTask<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_values.ContainsKey(Normalize(path)));
        public ValueTask DeleteFileAsync(string path, CancellationToken cancellationToken = default)
        { _values.Remove(Normalize(path)); WriteCount++; return ValueTask.CompletedTask; }
        private string Read(string path) => _values.TryGetValue(Normalize(path), out string? value)
            ? value
            : throw new FileNotFoundException(path);
        private static string Normalize(string path) => Path.GetFullPath(path);
        private static StringComparer PathComparer => OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    }
}
