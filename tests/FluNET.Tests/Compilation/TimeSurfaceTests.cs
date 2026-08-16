using FluNET.Capabilities;
using FluNET.Compilation;
using FluNET.Context;
using FluNET.Tooling;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class TimeSurfaceTests
{
    [Test]
    public async Task NowUsesTheHostClock()
    {
        DateTimeOffset instant = new(2026, 8, 16, 12, 34, 56, TimeSpan.Zero);
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(
            services => services.AddSingleton<IFluNetClock>(new FixedClock(instant)));

        SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync("NOW AS timestamp");

        Assert.That(execution.IsSuccess, Is.True, execution.Error?.ToString());
        Assert.That(execution.Result, Is.EqualTo(instant.ToString("O")));
    }

    [Test]
    public async Task WaitUsesTheHostDelayCapability()
    {
        CaptureDelay delay = new();
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(
            services => services.AddSingleton<IFluNetDelay>(delay));

        SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync("WAIT 250ms");

        Assert.That(execution.IsSuccess, Is.True,
            execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Diagnostics.Select(item => item.Message)));
        Assert.That(delay.Duration, Is.EqualTo(TimeSpan.FromMilliseconds(250)));
    }

    [Test]
    public void TimeFramesAreMappedToTheSystemCapability()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult compilation = context.CompileSurface("NOW AS timestamp\nWAIT 1s");

        Assert.That(compilation.IsValid, Is.True,
            string.Join(" | ", compilation.Diagnostics.Select(item => item.Message)));
        string graph = new SurfaceGraphExporter().ToDot(compilation);

        Assert.That(graph, Does.Contain("surface.system.now"));
        Assert.That(graph, Does.Contain("surface.system.wait"));
        Assert.That(graph, Does.Contain("system.time"));
        Assert.That(context.GetService<CapabilityRegistry>().TryResolve("system.time", out _), Is.True);
    }

    private sealed class FixedClock(DateTimeOffset value) : IFluNetClock
    {
        public DateTimeOffset UtcNow => value;
    }

    private sealed class CaptureDelay : IFluNetDelay
    {
        public TimeSpan Duration { get; private set; }

        public ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken = default)
        {
            Duration = duration;
            return ValueTask.CompletedTask;
        }
    }
}
