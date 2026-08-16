using FluNET.Capabilities;
using FluNET.Context;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class ProcessSurfaceTests
{
    [Test]
    public void ExecuteSupportsPortableWorkingDirectory()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();

        var compilation = context.CompileSurface("EXECUTE \"dotnet --info\" IN \"./tools\" AS result");

        Assert.That(compilation.IsValid, Is.True,
            string.Join(" | ", compilation.Lowering.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        Assert.That(compilation.Lowering.CanonicalSyntax.Commands.Single().AllTokens.Select(t => t.Text),
            Does.Contain("IN"));
    }

    [Test]
    public void ExecuteSupportsExplicitEnvironmentAssignments()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();

        var compilation = context.CompileSurface(
            "EXECUTE \"dotnet --info\" IN \"./tools\" ENV {DOTNET_NOLOGO=1, MODE=\"test run\"} AS result");

        Assert.That(compilation.IsValid, Is.True,
            string.Join(" | ", compilation.Lowering.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        string[] tokens = compilation.Lowering.CanonicalSyntax.Commands.Single().AllTokens.Select(t => t.Text).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(tokens, Does.Contain("IN"));
            Assert.That(tokens, Does.Contain("ENV"));
            Assert.That(tokens.Any(token => token.Contains("DOTNET_NOLOGO", StringComparison.OrdinalIgnoreCase)), Is.True);
        });
    }

    [Test]
    public async Task ExecutePassesWorkingDirectoryAndEnvironmentToRunner()
    {
        CaptureProcess runner = new();
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(
            services => services.AddSingleton<IFluNetProcessRunner>(runner));

        SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(
            "EXECUTE \"dotnet --info\" IN \"./tools\" ENV {MODE=\"test run\", FLAG=1} AS result");

        Assert.That(execution.IsSuccess, Is.True,
            execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Diagnostics.Select(d => d.Message)));
        Assert.Multiple(() =>
        {
            Assert.That(runner.Request!.FileName, Is.EqualTo("dotnet"));
            Assert.That(runner.Request.WorkingDirectory, Is.EqualTo("./tools"));
            Assert.That(runner.Request.Environment!["MODE"], Is.EqualTo("test run"));
            Assert.That(runner.Request.Environment["FLAG"], Is.EqualTo("1"));
        });
    }

    private sealed class CaptureProcess : IFluNetProcessRunner
    {
        public FluNetProcessRequest? Request { get; private set; }

        public ValueTask<FluNetProcessResult> RunAsync(
            FluNetProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return ValueTask.FromResult(new FluNetProcessResult(0, string.Empty, string.Empty, false));
        }
    }
}
