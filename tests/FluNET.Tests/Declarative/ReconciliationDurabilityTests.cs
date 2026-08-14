using FluNET.Capabilities;
using FluNET.Context;
using FluNET.Declarative.Reconciliation;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace FluNET.Tests.Declarative;

[TestFixture]
public sealed class ReconciliationDurabilityTests
{
    [Test]
    public async Task StoredBaselineTurnsIndependentSourceAndTargetChangesIntoConflict()
    {
        MemoryFiles files = new(new Dictionary<string, string>
        {
            ["target.json"] = "[{\"id\":1,\"name\":\"base\"}]",
            ["desired.json"] = "[{\"id\":1,\"name\":\"base\"}]"
        });
        InMemoryReconciliationStateStore state = new();
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(services =>
        {
            services.AddSingleton<IFluNetFileSystem>(files);
            services.AddSingleton<IReconciliationStateStore>(state);
        });

        ReconciliationRunResult first = (await context.ExecuteSyncAsync(
            "SYNC target.json WITH desired.json BY id")).Single();
        Assert.That(first.IsSuccess, Is.True);
        Assert.That(await state.GetAsync(first.Definition.Id), Is.Not.Null);

        files.Set("target.json", "[{\"id\":1,\"name\":\"target-change\"}]");
        files.Set("desired.json", "[{\"id\":1,\"name\":\"source-change\"}]");

        ReconciliationRunResult second = (await context.ExecuteSyncAsync(
            "SYNC target.json WITH desired.json BY id")).Single();

        Assert.Multiple(() =>
        {
            Assert.That(second.IsSuccess, Is.False);
            Assert.That(second.Applied, Is.False);
            Assert.That(second.Diff!.Conflicts, Is.EqualTo(1));
            Assert.That(second.Error, Is.TypeOf<ReconciliationConflictException>());
        });
    }

    [Test]
    public async Task DurableStoreRoundTripsChecksummedBaseline()
    {
        string directory = Path.Combine(Path.GetTempPath(), "FluNET_Reconcile_State_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            DurableReconciliationStateStore store = new(
                new DurableReconciliationStateOptions(directory),
                new AllowAllExecutionPolicy());
            ReconciliationBaselineState state = new(
                "sync-test",
                "file",
                Path.GetFullPath("target.json"),
                "id",
                [System.Text.Json.JsonSerializer.SerializeToElement(new { id = 1, name = "Ada" })],
                DateTimeOffset.UtcNow);

            await store.SetAsync(state);
            ReconciliationBaselineState? restored = await store.GetAsync("sync-test");

            Assert.Multiple(() =>
            {
                Assert.That(restored, Is.Not.Null);
                Assert.That(restored!.KeyField, Is.EqualTo("id"));
                Assert.That(restored.Records.Single().GetProperty("name").GetString(), Is.EqualTo("Ada"));
            });
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private sealed class MemoryFiles : IFluNetFileSystem
    {
        private readonly Dictionary<string, string> values;
        public MemoryFiles(IReadOnlyDictionary<string, string> seed) =>
            values = seed.ToDictionary(item => Path.GetFullPath(item.Key), item => item.Value, PathComparer);
        public void Set(string path, string value) => values[Path.GetFullPath(path)] = value;
        public Task<string[]> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(Read(path).Split('\n'));
        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(Read(path));
        public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
        { values[Path.GetFullPath(path)] = content; return Task.CompletedTask; }
        public Task WriteAllBytesAsync(string path, byte[] content, CancellationToken cancellationToken = default)
        { values[Path.GetFullPath(path)] = Encoding.UTF8.GetString(content); return Task.CompletedTask; }
        public ValueTask<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(values.ContainsKey(Path.GetFullPath(path)));
        public ValueTask DeleteFileAsync(string path, CancellationToken cancellationToken = default)
        { values.Remove(Path.GetFullPath(path)); return ValueTask.CompletedTask; }
        private string Read(string path) => values.TryGetValue(Path.GetFullPath(path), out string? value)
            ? value
            : throw new FileNotFoundException(path);
        private static StringComparer PathComparer => OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    }
}
