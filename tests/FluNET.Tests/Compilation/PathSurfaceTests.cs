using FluNET.Capabilities;
using FluNET.Context;
using FluNET.Tooling;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class PathSurfaceTests
{
    [Test]
    public void PathResolvesToTypedSystemCapability()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();

        var compilation = context.CompileSurface("PATH TEMP AS temporary");

        Assert.That(compilation.IsValid, Is.True,
            string.Join(" | ", compilation.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        Assert.That(compilation.BoundProgram!.Commands.Single().Frame.Id.Value,
            Is.EqualTo("surface.system.path"));
    }

    [TestCase("TEMP")]
    [TestCase("HOME")]
    [TestCase("CURRENT")]
    public async Task PathResolvesThroughHostProvider(string name)
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();

        var execution = await context.ExecuteSurfaceAsync($"PATH {name} AS value");

        Assert.That(execution.IsSuccess, Is.True,
            execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Diagnostics.Select(d => d.Message)));
        Assert.That(execution.Result, Is.TypeOf<string>());
        Assert.That((string)execution.Result!, Is.Not.Empty);
    }

    [Test]
    public void PathCapabilityIsDiscoverable()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();

        Assert.That(context.GetService<CapabilityRegistry>().Describe().Any(d => d.Id == "system.path"), Is.True);
    }

    [Test]
    public void PathAppearsInCapabilityGraph()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var compilation = context.CompileSurface("PATH TEMP AS temporary");

        Assert.That(new SurfaceGraphExporter().ToDot(compilation), Does.Contain("system.path"));
    }
}
