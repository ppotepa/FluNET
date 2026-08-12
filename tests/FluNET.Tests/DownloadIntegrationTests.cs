using System.Text.Json;
using FluNET.Capabilities;
using FluNET.Context;
using FluNET.Execution;
using FluNET.Prompt;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests;

/// <summary>End-to-end download tests with deterministic in-memory HTTP.</summary>
[TestFixture]
public sealed class DownloadIntegrationTests
{
    private FluNETContext _context = null!;
    private FakeHttpTransport _http = null!;
    private Engine _engine = null!;
    private string _directory = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"FluNET_DownloadIntegration_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _http = new FakeHttpTransport();
        _context = FluNETContext.Create(services => services.AddSingleton<IHttpTransport>(_http));
        _engine = _context.GetEngine();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    [Test]
    public async Task Download_JsonFilePreservesParseableContent()
    {
        const string uri = "https://example.test/data.json";
        _http.AddDownload(uri, "{\"name\":\"Test Data\",\"items\":[1,2,3]}");
        string destination = Path.Combine(_directory, "data.json");

        ExecutionResult execution = await _engine.ExecuteAsync(new ProcessedPrompt(
            $"DOWNLOAD [data] FROM {{{uri}}} TO {{{destination}}}."));

        using JsonDocument json = JsonDocument.Parse(await File.ReadAllTextAsync(destination));
        Assert.Multiple(() =>
        {
            Assert.That(execution.IsSuccess, Is.True, execution.Error?.Message);
            Assert.That(json.RootElement.GetProperty("name").GetString(), Is.EqualTo("Test Data"));
            Assert.That(json.RootElement.GetProperty("items").GetArrayLength(), Is.EqualTo(3));
        });
    }

    [Test]
    public async Task Download_BinaryFilePreservesBytes()
    {
        const string uri = "https://example.test/image.png";
        byte[] expected = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
        _http.AddDownload(uri, expected);
        string destination = Path.Combine(_directory, "image.png");

        ExecutionResult execution = await _engine.ExecuteAsync(new ProcessedPrompt(
            $"DOWNLOAD [image] FROM {{{uri}}} TO {{{destination}}}."));

        Assert.Multiple(() =>
        {
            Assert.That(execution.IsSuccess, Is.True, execution.Error?.Message);
            Assert.That(File.ReadAllBytes(destination), Is.EqualTo(expected));
        });
    }

    [Test]
    public async Task RestrictedPolicyBlocksDestinationOutsideAllowedRoot()
    {
        const string uri = "https://allowed.example/file.txt";
        _http.AddDownload(uri, "content");
        string outside = Path.Combine(Path.GetTempPath(), $"outside-{Guid.NewGuid():N}.txt");
        using FluNETContext restrictedContext = FluNETContext.Create(services =>
        {
            services.AddSingleton<IExecutionPolicy>(new RestrictedExecutionPolicy(
                [_directory],
                ["allowed.example"]));
            services.AddSingleton<IHttpTransport>(_http);
        });

        ExecutionResult execution = await restrictedContext.GetEngine().ExecuteAsync(new ProcessedPrompt(
            $"DOWNLOAD [file] FROM {{{uri}}} TO {{{outside}}}."));

        Assert.Multiple(() =>
        {
            Assert.That(execution.Error?.Kind, Is.EqualTo(ExecutionFailureKind.Capability));
            Assert.That(execution.Error?.Code, Is.EqualTo("FLN230"));
            Assert.That(File.Exists(outside), Is.False);
        });
    }
}
