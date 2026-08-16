using FluNET.Capabilities;
using FluNET.Compilation;
using FluNET.Context;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class EventSinkSurfaceTests
{
    [Test]
    public async Task EmitUsesTheHostOwnedEventSink()
    {
        CapturingSink sink = new();
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(services =>
            services.AddSingleton<IFluNetEventSink>(sink));

        SurfaceCompilationResult compilation = context.CompileSurface(
            "LET payload = '{\"kind\":\"created\"}'\nEMIT [payload] TO https://hooks.example.test/events");

        Assert.That(compilation.IsValid, Is.True,
            string.Join(" | ", compilation.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        Assert.That(compilation.Lowering.CanonicalSyntax.Commands.Last().AllTokens.First().Text, Is.EqualTo("EMITEVENT"));

        SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(
            "LET payload = '{\"kind\":\"created\"}'\nEMIT [payload] TO https://hooks.example.test/events");

        Assert.That(execution.IsSuccess, Is.True, execution.Error?.ToString());
        Assert.That(sink.Endpoint, Is.EqualTo(new Uri("https://hooks.example.test/events")));
        Assert.That(sink.Payload, Is.EqualTo("{\"kind\":\"created\"}"));
        Assert.That(execution.Result, Is.EqualTo("accepted"));
    }

    private sealed class CapturingSink : IFluNetEventSink
    {
        public Uri? Endpoint { get; private set; }
        public string? Payload { get; private set; }

        public ValueTask<string> EmitAsync(Uri endpoint, string payload, SecretValue? credential = null, CancellationToken cancellationToken = default)
        {
            Endpoint = endpoint;
            Payload = payload;
            return ValueTask.FromResult("accepted");
        }
    }
}
