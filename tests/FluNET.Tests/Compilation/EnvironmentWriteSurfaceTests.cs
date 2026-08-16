using FluNET.Capabilities;
using FluNET.Context;
using FluNET.Tooling;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class EnvironmentWriteSurfaceTests
{
    [Test]
    public void SetEnvironmentLowersToDedicatedCapabilityFrame()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var compilation = context.CompileSurface("SET ENV MODE TO \"test\" AS changed");

        Assert.That(compilation.IsValid, Is.True,
            string.Join(" | ", compilation.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        Assert.That(compilation.BoundProgram!.Commands.Single().Frame.Id.Value,
            Is.EqualTo("surface.system.environment.write"));
    }

    [Test]
    public async Task DefaultHostDeniesEnvironmentMutation()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var execution = await context.ExecuteSurfaceAsync("SET ENV MODE TO \"test\" AS changed");

        Assert.That(execution.IsSuccess, Is.False);
        Assert.That(execution.Error, Is.TypeOf<CapabilityDeniedException>());
    }

    [Test]
    public async Task HostWriterCanOptInToEnvironmentMutation()
    {
        CapturingWriter writer = new();
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(
            services => services.AddSingleton<IEnvironmentWriter>(writer));

        var execution = await context.ExecuteSurfaceAsync("SET ENV MODE TO \"test run\" AS changed");

        Assert.That(execution.IsSuccess, Is.True, execution.Error?.ToString());
        Assert.That(writer.Name, Is.EqualTo("MODE"));
        Assert.That(writer.Value, Is.EqualTo("test run"));
    }

    private sealed class CapturingWriter : IEnvironmentWriter
    {
        public string? Name { get; private set; }
        public string? Value { get; private set; }
        public void Set(string name, string value) { Name = name; Value = value; }
    }
}
