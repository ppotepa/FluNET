using FluNET.Capabilities;
using FluNET.Context;
using FluNET.Declarative.Reconciliation;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace FluNET.Tests.Declarative;

[TestFixture]
public sealed class ReconciliationRecoveryTests
{
    [Test]
    public async Task IncompleteApplyingCheckpointIsRecoveredByReObservation()
    {
        MemoryFiles files = new(new Dictionary<string, string>
        {
            ["target.json"] = "[{\"id\":1,\"name\":\"same\"}]",
            ["desired.json"] = "[{\"id\":1,\"name\":\"same\"}]"
        });
        InMemoryReconciliationCheckpointStore checkpoints = new();
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(services =>
        {
            services.AddSingleton<IFluNetFileSystem>(files);
            services.AddSingleton<IReconciliationCheckpointStore>(checkpoints);
            services.AddSingleton<IReconciliationStateStore, InMemoryReconciliationStateStore>();
        });
        SyncDefinition definition = context.CompileSync("SYNC target.json WITH desired.json BY id").Definitions.Single();
        await checkpoints.AppendAsync(new(Guid.NewGuid(), definition.Id, ReconciliationCheckpointPhase.Applying, DateTimeOffset.UtcNow, 7));

        ReconciliationRunResult result = (await context.ExecuteSyncAsync("SYNC target.json WITH desired.json BY id")).Single();
        IReadOnlyList<ReconciliationCheckpoint> history = await checkpoints.ReadAsync(definition.Id);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Applied, Is.False);
            Assert.That(history.Any(item => item.Phase == ReconciliationCheckpointPhase.Recovering), Is.True);
            Assert.That(history.Last().Phase, Is.EqualTo(ReconciliationCheckpointPhase.BaselineSaved));
        });
    }

    [Test]
    public async Task PhysicalFileSystemAtomicWriteLeavesNoSiblingTemporaryFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), "FluNET_Atomic_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "state.json");
        try
        {
            PhysicalFluNetFileSystem files = new(new RestrictedExecutionPolicy([directory], Array.Empty<string>()));
            await files.WriteAllTextAsync(path, "new-value");
            Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo("new-value"));
            Assert.That(Directory.EnumerateFiles(directory, "*.flunet-*.tmp"), Is.Empty);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private sealed class MemoryFiles : IFluNetFileSystem
    {
        private readonly Dictionary<string, string> values;
        public MemoryFiles(IReadOnlyDictionary<string, string> seed) => values = seed.ToDictionary(item => Path.GetFullPath(item.Key), item => item.Value, OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        public Task<string[]> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(Read(path).Split('\n'));
        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(Read(path));
        public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default) { values[Path.GetFullPath(path)] = content; return Task.CompletedTask; }
        public Task WriteAllBytesAsync(string path, byte[] content, CancellationToken cancellationToken = default) { values[Path.GetFullPath(path)] = Encoding.UTF8.GetString(content); return Task.CompletedTask; }
        public ValueTask<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default) => ValueTask.FromResult(values.ContainsKey(Path.GetFullPath(path)));
        public ValueTask DeleteFileAsync(string path, CancellationToken cancellationToken = default) { values.Remove(Path.GetFullPath(path)); return ValueTask.CompletedTask; }
        private string Read(string path) => values.TryGetValue(Path.GetFullPath(path), out string? value) ? value : throw new FileNotFoundException(path);
    }
}
