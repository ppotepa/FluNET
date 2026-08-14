using FluNET.Capabilities;
using FluNET.Context;
using FluNET.Declarative.Reconciliation;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace FluNET.Tests.Declarative;

[TestFixture]
public sealed class ReconciliationObservationTests
{
    [Test]
    public async Task LocalJsonArrayIsObservedAsKeyedStateWithoutUsingLoadConfig()
    {
        MemoryFiles files = new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["users.json"] = "[{\"id\":2,\"name\":\"B\"},{\"id\":1,\"name\":\"A\"}]"
        });
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(services =>
            services.AddSingleton<IFluNetFileSystem>(files));

        ObservedStateSnapshot snapshot = await context.ObserveResourceAsync("users.json", "id");

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Records, Has.Count.EqualTo(2));
            Assert.That(snapshot.Records.Select(item => item.Key), Is.EqualTo(new[] { "1", "2" }));
            Assert.That(snapshot.Identity.Scheme, Is.EqualTo("file"));
        });
    }

    [Test]
    public void DuplicateKeysAreRejectedAtObservationBoundary()
    {
        JsonElement[] values =
        [
            JsonSerializer.SerializeToElement(new { id = 1 }),
            JsonSerializer.SerializeToElement(new { id = 1 })
        ];

        Assert.Throws<FormatException>(() =>
            new ObservedStateSnapshot(new ResourceIdentity("memory", "users"), "id", values));
    }

    private sealed class MemoryFiles(Dictionary<string, string> values) : IFluNetFileSystem
    {
        private readonly Dictionary<string, string> _values = values;
        public Task<string[]> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult((Get(path)).Split('\n'));
        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(Get(path));
        public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
        { _values[path] = content; return Task.CompletedTask; }
        public Task WriteAllBytesAsync(string path, byte[] content, CancellationToken cancellationToken = default)
        { _values[path] = System.Text.Encoding.UTF8.GetString(content); return Task.CompletedTask; }
        public ValueTask<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_values.ContainsKey(path));
        public ValueTask DeleteFileAsync(string path, CancellationToken cancellationToken = default)
        { _values.Remove(path); return ValueTask.CompletedTask; }
        private string Get(string path) => _values.TryGetValue(path, out string? value)
            ? value
            : throw new FileNotFoundException(path);
    }
}
