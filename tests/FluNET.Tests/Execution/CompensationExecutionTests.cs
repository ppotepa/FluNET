using FluNET.Capabilities;
using FluNET.Context;
using FluNET.Execution.Compensation;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace FluNET.Tests.Execution;

[TestFixture]
public sealed class CompensationExecutionTests
{
    [Test]
    public void NonReversibleEffectCannotOptIntoBuiltInCompensation()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();

        CompensatableCompilationResult compilation =
            context.CompileCompensatableSurface("SAY \"sent\" COMPENSATE");

        Assert.Multiple(() =>
        {
            Assert.That(compilation.IsValid, Is.False);
            Assert.That(compilation.Diagnostics.Any(item => item.Code == "FLN360"), Is.True);
        });
    }

    [Test]
    public async Task LaterFatalEffectRestoresPreviousSaveContent()
    {
        MemoryFiles files = new(new Dictionary<string, string>
        {
            ["input.txt"] = "new-value",
            ["output.txt"] = "old-value"
        });
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(services =>
        {
            services.AddSingleton<IFluNetFileSystem>(files);
            services.AddSingleton<IHttpTransport>(new FailingPostTransport());
        });

        CompensationExecutionResult result = await context.ExecuteCompensatableSurfaceAsync(
            "LOAD input.txt AS value; SAVE value TO output.txt COMPENSATE; POST value TO https://api.example.test/fail");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.WasCompensated, Is.True);
            Assert.That(result.Compensation.Single().Restored, Is.True);
            Assert.That(files.Get("output.txt"), Is.EqualTo("old-value"));
        });
    }

    private sealed class FailingPostTransport : IHttpTransport
    {
        public Task<byte[]> GetBytesAsync(Uri uri, CancellationToken cancellationToken = default) =>
            Task.FromResult(Array.Empty<byte>());

        public Task<string> PostJsonAsync(
            Uri uri,
            string json,
            CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("simulated POST failure");
    }

    private sealed class MemoryFiles : IFluNetFileSystem
    {
        private readonly Dictionary<string, string> values;

        public MemoryFiles(IReadOnlyDictionary<string, string> initial) =>
            values = initial.ToDictionary(item => Normalize(item.Key), item => item.Value, PathComparer);

        public string Get(string path) => values[Normalize(path)];

        public Task<string[]> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(Read(path).Split('\n'));

        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(Read(path));

        public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
        {
            values[Normalize(path)] = content;
            return Task.CompletedTask;
        }

        public Task WriteAllBytesAsync(string path, byte[] content, CancellationToken cancellationToken = default)
        {
            values[Normalize(path)] = Encoding.UTF8.GetString(content);
            return Task.CompletedTask;
        }

        public ValueTask<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(values.ContainsKey(Normalize(path)));

        public ValueTask DeleteFileAsync(string path, CancellationToken cancellationToken = default)
        {
            values.Remove(Normalize(path));
            return ValueTask.CompletedTask;
        }

        private string Read(string path) => values.TryGetValue(Normalize(path), out string? value)
            ? value
            : throw new FileNotFoundException(path);

        private static string Normalize(string path) => Path.GetFullPath(path);
        private static StringComparer PathComparer => OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    }
}
