using FluNET.Capabilities;
using FluNET.Context;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class ProcessSessionSurfaceTests
{
    [Test]
    public void SessionCommandsLowerToStableFrames()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();

        var compilation = context.CompileSurface("""
START "dotnet --info" AS session
SEND "status" TO [session] AS response
STOP [session] AS result
""");

        Assert.That(compilation.IsValid, Is.True,
            string.Join(" | ", compilation.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        Assert.That(compilation.Plan!.Steps.Select(step => step.Command.Frame.Id.Value),
            Is.EqualTo(new[]
            {
                "system.process.session.start",
                "system.process.session.send",
                "system.process.session.stop"
            }));
    }

    [Test]
    public async Task SessionCommandsUseTheHostRegistry()
    {
        CaptureSessions sessions = new();
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(
            services => services.AddSingleton<IFluNetProcessSessionRegistry>(sessions));

        var execution = await context.ExecuteSurfaceAsync("""
START "dotnet --info" AS session
SEND "status" TO [session] AS response
STOP [session] AS result
""");

        Assert.That(execution.IsSuccess, Is.True,
            execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Diagnostics.Select(d => d.Message)));
        Assert.Multiple(() =>
        {
            Assert.That(sessions.Started, Is.True);
            Assert.That(sessions.Input, Is.EqualTo("status"));
            Assert.That(sessions.StoppedId, Is.EqualTo("session-1"));
        });
    }

    [Test]
    public void StartSupportsWorkingDirectoryAndEnvironment()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();

        var compilation = context.CompileSurface(
            "START \"dotnet --info\" IN \"./tools\" ENV {MODE='test'} AS session");

        Assert.That(compilation.IsValid, Is.True,
            string.Join(" | ", compilation.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        string[] tokens = compilation.Lowering.CanonicalSyntax.Commands.Single().AllTokens.Select(t => t.Text).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(tokens, Does.Contain("IN"));
            Assert.That(tokens, Does.Contain("ENV"));
        });
    }

    private sealed class CaptureSessions : IFluNetProcessSessionRegistry
    {
        public bool Started { get; private set; }
        public string? Input { get; private set; }
        public string? StoppedId { get; private set; }

        public ValueTask<FluNetProcessSessionOutput> StartAsync(FluNetProcessRequest request, CancellationToken cancellationToken = default)
        {
            Started = true;
            return ValueTask.FromResult(new FluNetProcessSessionOutput("session-1", "", "", true));
        }

        public ValueTask<FluNetProcessSessionOutput> SendAsync(string sessionId, string input, CancellationToken cancellationToken = default)
        {
            Input = input;
            return ValueTask.FromResult(new FluNetProcessSessionOutput(sessionId, "ok", "", true));
        }

        public ValueTask<FluNetProcessResult> StopAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            StoppedId = sessionId;
            return ValueTask.FromResult(new FluNetProcessResult(0, "", "", false));
        }
    }
}
