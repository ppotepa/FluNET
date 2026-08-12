using FluNET.Capabilities;
using FluNET.Context;
using FluNET.Execution;
using FluNET.Prompt;
using FluNET.Syntax.Verbs;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests;

[TestFixture]
public sealed class DownloadCommandTests
{
    private const string Source = "https://example.test/files/data.txt";
    private FluNETContext _context = null!;
    private FakeHttpTransport _http = null!;
    private Engine _engine = null!;
    private string _directory = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"FluNET_Download_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _http = new FakeHttpTransport();
        _http.AddDownload(Source, "deterministic download content");
        _context = FluNETContext.Create(services => services.AddSingleton<IHttpTransport>(_http));
        _engine = _context.GetEngine();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    [TestCase("DOWNLOAD")]
    [TestCase("PULL")]
    [TestCase("GRAB")]
    [TestCase("OBTAIN")]
    public async Task Download_UsesInjectedTransportAndWritesDestination(string verb)
    {
        string destination = Path.Combine(_directory, $"{verb}.txt");
        ProcessedPrompt prompt = new($"{verb} [file] FROM {{{Source}}} TO {{{destination}}}.");

        ExecutionResult execution = await _engine.ExecuteAsync(prompt);

        Assert.Multiple(() =>
        {
            Assert.That(execution.IsSuccess, Is.True, execution.Error?.Message);
            Assert.That(execution.Sentence?.Root, Is.InstanceOf<DownloadFile>());
            Assert.That(execution.Result, Is.InstanceOf<FileInfo>());
            Assert.That(File.ReadAllText(destination), Is.EqualTo("deterministic download content"));
        });
    }

    [Test]
    public async Task Download_ResolvesSourceAndDestinationVariables()
    {
        string destination = Path.Combine(_directory, "variables.txt");
        _engine.RegisterVariable("source", Source);
        _engine.RegisterVariable("destination", destination);

        ExecutionResult execution = await _engine.ExecuteAsync(
            new ProcessedPrompt("DOWNLOAD [file] FROM [source] TO [destination]."));

        Assert.Multiple(() =>
        {
            Assert.That(execution.IsSuccess, Is.True, execution.Error?.Message);
            Assert.That(File.ReadAllText(destination), Is.EqualTo("deterministic download content"));
        });
    }

    [Test]
    public async Task Download_TransportFailureIsReturnedAsStructuredError()
    {
        _http.Failure = new HttpRequestException("simulated network failure");
        string destination = Path.Combine(_directory, "failure.txt");

        ExecutionResult execution = await _engine.ExecuteAsync(new ProcessedPrompt(
            $"DOWNLOAD [file] FROM {{{Source}}} TO {{{destination}}}."));

        Assert.Multiple(() =>
        {
            Assert.That(execution.IsSuccess, Is.False);
            Assert.That(execution.Error?.Kind, Is.EqualTo(ExecutionFailureKind.Execution));
            Assert.That(execution.Error?.Code, Is.EqualTo("FLN200"));
            Assert.That(execution.Error?.Message, Does.Contain("simulated network failure"));
            Assert.That(File.Exists(destination), Is.False);
        });
    }

    [Test]
    public async Task Download_CancellationIsPropagatedAsStructuredError()
    {
        _http.WaitForCancellation = true;
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(25));
        string destination = Path.Combine(_directory, "cancelled.txt");

        ExecutionResult execution = await _engine.ExecuteAsync(new ProcessedPrompt(
            $"DOWNLOAD [file] FROM {{{Source}}} TO {{{destination}}}."), cancellation.Token);

        Assert.Multiple(() =>
        {
            Assert.That(execution.Error?.Kind, Is.EqualTo(ExecutionFailureKind.Cancelled));
            Assert.That(execution.Error?.Code, Is.EqualTo("FLN201"));
            Assert.That(File.Exists(destination), Is.False);
        });
    }

    [TestCase("DOWNLOAD [file] TO {out.txt}.")]
    [TestCase("DOWNLOAD FROM {https://example.test/file.txt}.")]
    [TestCase("DOWNLOAD [file] FROM {not-a-url}.")]
    public void Download_InvalidShapeOrUrlDoesNotExecute(string text)
    {
        PromptAnalysis analysis = _engine.Analyze(new ProcessedPrompt(text));

        Assert.That(analysis.IsValid, Is.False);
    }
}
