using FluNET.Capabilities;
using FluNET.Context;
using FluNET.Declarative.Reconciliation;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;

namespace FluNET.Tests.Declarative;

[TestFixture]
public sealed class ReconciliationConflictPolicyTests
{
    [TestCase("FAIL", ReconciliationConflictPolicy.Fail)]
    [TestCase("KEEP TARGET", ReconciliationConflictPolicy.KeepTarget)]
    [TestCase("KEEP SOURCE", ReconciliationConflictPolicy.KeepSource)]
    public void CompilerParsesExplicitConflictPolicy(string source, ReconciliationConflictPolicy expected)
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SyncCompilationResult result = context.CompileSync($"SYNC target.json WITH desired.json BY id ON CONFLICT {source}");
        Assert.That(result.IsValid, Is.True, string.Join(" | ", result.Diagnostics.Select(item => item.Message)));
        Assert.That(result.Definitions.Single().Goal.ConflictPolicy, Is.EqualTo(expected));
    }

    [Test]
    public async Task KeepTargetPreservesConflictingRecordButAppliesIndependentDesiredChange()
    {
        MemoryFiles files = new(new Dictionary<string, string>
        {
            ["target.json"] = "[{\"id\":1,\"name\":\"base\"}]",
            ["desired.json"] = "[{\"id\":1,\"name\":\"base\"}]"
        });
        using FluNETContext context = Create(files);
        await context.ExecuteSyncAsync("SYNC target.json WITH desired.json BY id");
        files.Set("target.json", "[{\"id\":1,\"name\":\"local\"}]");
        files.Set("desired.json", "[{\"id\":1,\"name\":\"remote\"},{\"id\":2,\"name\":\"new\"}]");

        ReconciliationRunResult result = (await context.ExecuteSyncAsync(
            "SYNC target.json WITH desired.json BY id ON CONFLICT KEEP TARGET")).Single();

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Applied, Is.True);
        using JsonDocument document = JsonDocument.Parse(files.Get("target.json"));
        Assert.Multiple(() =>
        {
            Assert.That(document.RootElement.GetArrayLength(), Is.EqualTo(2));
            Assert.That(document.RootElement[0].GetProperty("name").GetString(), Is.EqualTo("local"));
            Assert.That(document.RootElement[1].GetProperty("name").GetString(), Is.EqualTo("new"));
        });
    }

    [Test]
    public async Task KeepSourceMakesDesiredRecordAuthoritativeOnConflict()
    {
        MemoryFiles files = new(new Dictionary<string, string>
        {
            ["target.json"] = "[{\"id\":1,\"name\":\"base\"}]",
            ["desired.json"] = "[{\"id\":1,\"name\":\"base\"}]"
        });
        using FluNETContext context = Create(files);
        await context.ExecuteSyncAsync("SYNC target.json WITH desired.json BY id");
        files.Set("target.json", "[{\"id\":1,\"name\":\"local\"}]");
        files.Set("desired.json", "[{\"id\":1,\"name\":\"remote\"}]");

        ReconciliationRunResult result = (await context.ExecuteSyncAsync(
            "SYNC target.json WITH desired.json BY id ON CONFLICT KEEP SOURCE")).Single();

        Assert.That(result.IsSuccess, Is.True);
        using JsonDocument document = JsonDocument.Parse(files.Get("target.json"));
        Assert.That(document.RootElement[0].GetProperty("name").GetString(), Is.EqualTo("remote"));
    }

    private static FluNETContext Create(MemoryFiles files) => SurfaceCompilationExtensions.CreateSurfaceContext(services =>
    {
        services.AddSingleton<IFluNetFileSystem>(files);
        services.AddSingleton<IReconciliationStateStore, InMemoryReconciliationStateStore>();
    });

    private sealed class MemoryFiles : IFluNetFileSystem
    {
        private readonly Dictionary<string, string> values;
        public MemoryFiles(IReadOnlyDictionary<string, string> seed) => values = seed.ToDictionary(item => Path.GetFullPath(item.Key), item => item.Value, PathComparer);
        public void Set(string path, string value) => values[Path.GetFullPath(path)] = value;
        public string Get(string path) => values[Path.GetFullPath(path)];
        public Task<string[]> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(Read(path).Split('\n'));
        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(Read(path));
        public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default) { values[Path.GetFullPath(path)] = content; return Task.CompletedTask; }
        public Task WriteAllBytesAsync(string path, byte[] content, CancellationToken cancellationToken = default) { values[Path.GetFullPath(path)] = Encoding.UTF8.GetString(content); return Task.CompletedTask; }
        public ValueTask<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default) => ValueTask.FromResult(values.ContainsKey(Path.GetFullPath(path)));
        public ValueTask DeleteFileAsync(string path, CancellationToken cancellationToken = default) { values.Remove(Path.GetFullPath(path)); return ValueTask.CompletedTask; }
        private string Read(string path) => values.TryGetValue(Path.GetFullPath(path), out string? value) ? value : throw new FileNotFoundException(path);
        private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    }
}
