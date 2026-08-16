using FluNET.Capabilities;
using FluNET.Compilation;
using FluNET.Context;
using FluNET.Tooling;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class ClipboardSurfaceTests
{
    [Test]
    public async Task ReadClipboardUsesTheHostCapability()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(
            services => services.AddSingleton<IFluNetClipboard>(new FakeClipboard("copied text")));

        SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(
            "READ CLIPBOARD AS text");

        Assert.That(execution.IsSuccess, Is.True,
            execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Diagnostics.Select(item => item.Message)));
        Assert.That(execution.Result, Is.EqualTo("copied text"));
        Assert.That(context.GetService<CapabilityRegistry>().TryResolve("system.clipboard", out _), Is.True);
    }

    [Test]
    public async Task ReadClipboardReportsUnavailableHostCapability()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(
            services => services.AddSingleton<IFluNetClipboard>(new FakeClipboard(null)));

        SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(
            "READ CLIPBOARD AS text");

        Assert.That(execution.IsSuccess, Is.False);
        Assert.That(execution.Error, Is.TypeOf<CapabilityUnavailableException>());
    }

    [Test]
    public async Task CopyToClipboardUsesTheHostWriterCapability()
    {
        CaptureClipboard clipboard = new();
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(
            services =>
            {
                services.AddSingleton<IFluNetClipboard>(new FakeClipboard("unused"));
                services.AddSingleton<IFluNetClipboardWriter>(clipboard);
            });

        SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(
            "COPY \"hello from FluNET\" TO CLIPBOARD");

        Assert.That(execution.IsSuccess, Is.True,
            execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Diagnostics.Select(item => item.Message)));
        Assert.That(execution.Result, Is.EqualTo("hello from FluNET"));
        Assert.That(clipboard.Value, Is.EqualTo("hello from FluNET"));
    }

    [Test]
    public void ClipboardFramesAreRepresentedByTheCapabilityGraph()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult compilation = context.CompileSurface(
            "READ CLIPBOARD AS text\nCOPY [text] TO CLIPBOARD");

        Assert.That(compilation.IsValid, Is.True,
            string.Join(" | ", compilation.Diagnostics.Select(item => item.Message)));
        string graph = new SurfaceGraphExporter().ToDot(compilation);

        Assert.That(graph, Does.Contain("surface.system.clipboard.read"));
        Assert.That(graph, Does.Contain("surface.system.clipboard.write"));
        Assert.That(graph, Does.Contain("system.clipboard"));
    }

    private sealed class FakeClipboard(string? value) : IFluNetClipboard
    {
        public ValueTask<string?> ReadTextAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(value);
    }

    private sealed class CaptureClipboard : IFluNetClipboardWriter
    {
        public string? Value { get; private set; }

        public ValueTask WriteTextAsync(string value, CancellationToken cancellationToken = default)
        {
            Value = value;
            return ValueTask.CompletedTask;
        }
    }
}
