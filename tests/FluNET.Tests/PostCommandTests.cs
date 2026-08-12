using FluNET.Capabilities;
using FluNET.Context;
using FluNET.Execution;
using FluNET.Prompt;
using FluNET.Syntax.Verbs;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests;

[TestFixture]
public sealed class PostCommandTests
{
    private FluNETContext _context = null!;
    private FakeHttpTransport _http = null!;
    private Engine _engine = null!;

    [SetUp]
    public void SetUp()
    {
        _http = new FakeHttpTransport();
        _context = FluNETContext.Create(services => services.AddSingleton<IHttpTransport>(_http));
        _engine = _context.GetEngine();
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    [Test]
    public async Task Post_JsonReferenceUsesInjectedTransport()
    {
        const string json = "{\"name\":\"test\",\"value\":42}";
        const string endpoint = "https://example.test/post";

        ExecutionResult execution = await _engine.ExecuteAsync(
            new ProcessedPrompt($"POST {json} TO {{{endpoint}}}."));

        Assert.Multiple(() =>
        {
            Assert.That(execution.IsSuccess, Is.True, execution.Error?.Message);
            Assert.That(execution.Sentence?.Root, Is.InstanceOf<PostJson>());
            Assert.That(execution.Result, Is.EqualTo(_http.PostResponse));
            Assert.That(_http.Posts, Has.Count.EqualTo(1));
            Assert.That(_http.Posts[0].Uri, Is.EqualTo(new Uri(endpoint)));
            Assert.That(_http.Posts[0].Json, Is.EqualTo(json));
        });
    }

    [Test]
    public async Task Post_ResolvesPayloadAndEndpointVariables()
    {
        const string json = "{\"active\":true}";
        const string endpoint = "https://example.test/variables";
        _engine.RegisterVariable("payload", json);
        _engine.RegisterVariable("endpoint", endpoint);

        ExecutionResult execution = await _engine.ExecuteAsync(
            new ProcessedPrompt("POST [payload] TO [endpoint]."));

        Assert.Multiple(() =>
        {
            Assert.That(execution.IsSuccess, Is.True, execution.Error?.Message);
            Assert.That(_http.Posts.Single().Json, Is.EqualTo(json));
            Assert.That(_http.Posts.Single().Uri, Is.EqualTo(new Uri(endpoint)));
        });
    }

    [Test]
    public async Task Post_TransportFailureIsNotDisguisedAsNull()
    {
        _http.Failure = new HttpRequestException("simulated POST failure");

        ExecutionResult execution = await _engine.ExecuteAsync(new ProcessedPrompt(
            "POST {\"ok\":true} TO {https://example.test/failure}."));

        Assert.Multiple(() =>
        {
            Assert.That(execution.IsSuccess, Is.False);
            Assert.That(execution.Result, Is.Null);
            Assert.That(execution.Error?.Kind, Is.EqualTo(ExecutionFailureKind.Execution));
            Assert.That(execution.Error?.Message, Does.Contain("simulated POST failure"));
        });
    }
}
