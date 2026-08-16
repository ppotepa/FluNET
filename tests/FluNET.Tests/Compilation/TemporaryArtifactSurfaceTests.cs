using FluNET.Capabilities;
using FluNET.Context;
using FluNET.Tooling;
using FluNET.Variables;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class TemporaryArtifactSurfaceTests
{
    [TestCase("TEMP FILE AS artifact", "surface.system.temp.file")]
    [TestCase("TEMP FILE .json AS artifact", "surface.system.temp.file")]
    [TestCase("TEMP DIRECTORY AS workspace", "surface.system.temp.directory")]
    public void TemporarySyntaxLowersToTypedFrame(string source, string frame)
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var compilation = context.CompileSurface(source);

        Assert.That(compilation.IsValid, Is.True,
            string.Join(" | ", compilation.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        Assert.That(compilation.BoundProgram!.Commands.Single().Frame.Id.Value, Is.EqualTo(frame));
    }

    [Test]
    public void CleanupSyntaxLowersToTypedFrame()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var compilation = context.CompileSurface("TEMP FILE AS artifact\nCLEANUP [artifact]");

        Assert.That(compilation.IsValid, Is.True,
            string.Join(" | ", compilation.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        Assert.That(compilation.BoundProgram!.Commands.Last().Frame.Id.Value,
            Is.EqualTo("surface.system.temp.cleanup"));
    }

    [Test]
    public async Task TemporaryFileAndDirectoryAreCreatedByHostCapability()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var file = await context.ExecuteSurfaceAsync("TEMP FILE .json AS artifact");
        var directory = await context.ExecuteSurfaceAsync("TEMP DIRECTORY AS workspace");

        Assert.That(file.IsSuccess, Is.True,
            file.Error?.ToString() ?? string.Join(" | ", file.Compilation.Lowering.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        Assert.That(directory.IsSuccess, Is.True, directory.Error?.ToString() ?? string.Join(" | ", directory.Compilation.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        Assert.That(File.Exists((string)file.Result!), Is.True);
        Assert.That(Directory.Exists((string)directory.Result!), Is.True);

        var cleanup = await context.ExecuteSurfaceAsync(
            "CLEANUP [artifact]\nCLEANUP [workspace]");

        Assert.That(cleanup.IsSuccess, Is.True,
            cleanup.Error?.ToString() ?? string.Join(" | ", cleanup.Compilation.Lowering.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        Assert.That(File.Exists((string)file.Result!), Is.False);
        Assert.That(Directory.Exists((string)directory.Result!), Is.False);
    }

    [Test]
    public async Task SaveCanUseATemporaryFileVariableAsItsTarget()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();

        var execution = await context.ExecuteSurfaceAsync(
            "TEMP FILE .txt AS artifact\nSAVE \"temporary content\" TO [artifact]");

        Assert.That(execution.IsSuccess, Is.True, execution.Error?.ToString());
        string saved = context.GetService<IVariableResolver>().Resolve<string>("[artifact]")!;
        Assert.That(await File.ReadAllTextAsync(saved), Is.EqualTo("temporary content"));
    }

    [Test]
    public void TemporaryCapabilityIsDiscoverable()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();

        Assert.That(context.GetService<CapabilityRegistry>().TryResolve("system.temp", out _), Is.True);
        Assert.That(new SurfaceGraphExporter().ToDot(context.CompileSurface("TEMP FILE AS artifact")), Does.Contain("system.temp"));
    }

    [Test]
    public async Task DisposingContextCleansOwnedArtifacts()
    {
        string path;
        using (FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext())
        {
            var execution = await context.ExecuteSurfaceAsync("TEMP FILE AS artifact");
            Assert.That(execution.IsSuccess, Is.True, execution.Error?.ToString());
            path = (string)execution.Result!;
            Assert.That(File.Exists(path), Is.True);
        }

        Assert.That(File.Exists(path), Is.False);
    }
}
