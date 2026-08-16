using FluNET.Compilation;
using FluNET.Context;
using FluNET.Tooling;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class BlobStorageSurfaceTests
{
    [Test]
    public async Task SaveAndGetUseTheBlobResourceProvider()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();

        SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(
            "SAVE \"hello\" TO blob:reports/latest\nGET blob:reports/latest AS report");

        Assert.That(execution.IsSuccess, Is.True,
            execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Diagnostics.Select(item => item.Message)));
        Assert.That(execution.Result, Is.EqualTo("hello"));
        Assert.That(execution.Compilation.Plan!.Steps[0].Command.Frame.Id.Value,
            Is.EqualTo("storage.blob.put"));
        Assert.That(execution.Compilation.Plan.Steps[1].Command.Frame.Id.Value,
            Is.EqualTo("storage.blob.get"));
    }

    [Test]
    public async Task DeleteBlobUsesAnOrderedWriteFrame()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();

        SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(
            "SAVE \"hello\" TO blob:reports/latest\nDELETE blob:reports/latest AS removed");

        Assert.That(execution.IsSuccess, Is.True,
            execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Diagnostics.Select(item => item.Message)));
        Assert.That(execution.Compilation.Plan!.Steps[1].Command.Frame.Id.Value,
            Is.EqualTo("storage.blob.delete"));
    }

    [Test]
    public async Task ListBlobSupportsPrefixAndTheNormalJsonPipeline()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();

        SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync("""
            SAVE "one" TO blob:reports/one.json
            SAVE "two" TO blob:reports/two.txt
            LIST BLOB "reports/" AS keys
            WHERE key MATCHES '*.json'
            """);

        Assert.That(execution.IsSuccess, Is.True,
            execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Diagnostics.Select(item => item.Message)));
        var keys = (System.Text.Json.JsonElement[])execution.Result!;
        Assert.That(keys, Has.Length.EqualTo(1));
        Assert.That(keys[0].GetProperty("key").GetString(), Is.EqualTo("reports/one.json"));
    }

    [Test]
    public void BlobNodesAppearInTheCapabilityGraph()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult compilation = context.CompileSurface(
            "SAVE \"hello\" TO blob:reports/latest\nGET blob:reports/latest AS report");

        Assert.That(compilation.IsValid, Is.True,
            string.Join(" | ", compilation.Diagnostics.Select(item => item.Message)));
        string graph = new SurfaceGraphExporter().ToDot(compilation);

        Assert.That(graph, Does.Contain("storage.blob"));
        Assert.That(context.GetService<FluNET.Capabilities.CapabilityRegistry>()
            .TryResolve("storage.blob", out _), Is.True);
    }
}
