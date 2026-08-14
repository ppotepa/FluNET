using FluNET.Capabilities;
using FluNET.Context;
using FluNET.Declarative;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace FluNET.Tests.Declarative;

[TestFixture]
public sealed class EnsureExecutionTests
{
    [Test]
    public async Task KeepVersionsCapturesPreviousTargetAfterSuccessfulChange()
    {
        MemoryFiles files = new("old-content");
        InMemoryEnsureVersionStore versions = new();
        FakeHttp http = new("{\"version\":2}");
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(services =>
        {
            services.AddSingleton<IFluNetFileSystem>(files);
            services.AddSingleton<IHttpTransport>(http);
            services.AddSingleton<IEnsureVersionStore>(versions);
        });

        IReadOnlyList<EnsureRunResult> runs = await context.ExecuteEnsureAsync("""
ENSURE backup.json CONTAINS https://api.example.test/config
KEEP 2 VERSIONS
""");

        Assert.Multiple(() =>
        {
            Assert.That(runs, Has.Count.EqualTo(1));
            Assert.That(runs[0].IsSuccess, Is.True, runs[0].Error?.Message);
            Assert.That(files.Content, Does.Contain("version"));
            Assert.That(versions.Snapshot("backup.json").Select(item => item.Content),
                Is.EqualTo(new[] { "old-content" }));
        });
    }

    [Test]
    public async Task NotifyOnFailureUsesConfiguredTextOutputNotifier()
    {
        MemoryFiles files = new("old-content");
        FailingHttp http = new();
        CapturingOutput output = new();
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(services =>
        {
            services.AddSingleton<IFluNetFileSystem>(files);
            services.AddSingleton<IHttpTransport>(http);
            services.AddSingleton<ITextOutput>(output);
        });

        IReadOnlyList<EnsureRunResult> runs = await context.ExecuteEnsureAsync("""
ENSURE backup.json CONTAINS https://api.example.test/config
NOTIFY ON FAILURE
""");

        Assert.Multiple(() =>
        {
            Assert.That(runs, Has.Count.EqualTo(1));
            Assert.That(runs[0].IsSuccess, Is.False);
            Assert.That(output.Messages, Has.Count.EqualTo(1));
            Assert.That(output.Messages[0], Does.Contain("ENSURE failed"));
            Assert.That(output.Messages[0], Does.Contain("backup.json"));
        });
    }

    private sealed class FakeHttp(string json) : IHttpTransport
    {
        public Task<byte[]> GetBytesAsync(Uri uri, CancellationToken cancellationToken = default) =>
            Task.FromResult(Encoding.UTF8.GetBytes(json));
        public Task<string> PostJsonAsync(
            Uri uri,
            string json,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FailingHttp : IHttpTransport
    {
        public Task<byte[]> GetBytesAsync(Uri uri, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("simulated source failure");
        public Task<string> PostJsonAsync(
            Uri uri,
            string json,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class MemoryFiles(string initial) : IFluNetFileSystem
    {
        public string Content { get; private set; } = initial;
        public Task<string[]> ReadAllLinesAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Content.Split('\n'));
        public Task<string> ReadAllTextAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Content);
        public Task WriteAllTextAsync(
            string path,
            string content,
            CancellationToken cancellationToken = default)
        {
            Content = content;
            return Task.CompletedTask;
        }
        public Task WriteAllBytesAsync(
            string path,
            byte[] content,
            CancellationToken cancellationToken = default)
        {
            Content = Encoding.UTF8.GetString(content);
            return Task.CompletedTask;
        }
        public ValueTask<bool> FileExistsAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(true);
        public ValueTask DeleteFileAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            Content = string.Empty;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CapturingOutput : ITextOutput
    {
        public List<string> Messages { get; } = [];
        public ValueTask WriteLineAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return ValueTask.CompletedTask;
        }
    }
}
