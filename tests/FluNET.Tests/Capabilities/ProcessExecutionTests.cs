using FluNET.Capabilities;
using FluNET.Context;

namespace FluNET.Tests.Capabilities;

[TestFixture]
public sealed class ProcessExecutionTests
{
    [Test]
    public async Task PhysicalRunnerExecutesWithoutAHostShell()
    {
        PhysicalFluNetProcessRunner runner = new();

        FluNetProcessResult result = await runner.RunAsync(
            new FluNetProcessRequest("dotnet", ["--version"]));

        Assert.That(result.TimedOut, Is.False);
        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(result.StandardError, Is.Empty);
    }

    [Test]
    public async Task DefaultContextDeniesProcessExecution()
    {
        using FluNETContext context = FluNETContext.Create();

        SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(
            "EXECUTE \"dotnet --version\" AS result");

        Assert.That(execution.IsSuccess, Is.False);
        Assert.That(execution.Error, Is.TypeOf<CapabilityDeniedException>());
    }

    [Test]
    public void RequestCanCarryWorkingDirectory()
    {
        FluNetProcessRequest request = new("dotnet", ["--version"], WorkingDirectory: Environment.CurrentDirectory);

        Assert.That(request.WorkingDirectory, Is.EqualTo(Environment.CurrentDirectory));
    }

    [Test]
    public void RequestCanCarryEnvironmentOverrides()
    {
        FluNetProcessRequest request = new(
            "dotnet",
            ["--version"],
            Environment: new Dictionary<string, string> { ["DOTNET_NOLOGO"] = "1" });

        Assert.That(request.Environment!["DOTNET_NOLOGO"], Is.EqualTo("1"));
    }

    [Test]
    public async Task PhysicalSessionStartsAndStopsThroughPortableProvider()
    {
        await using PhysicalFluNetProcessSessionRegistry sessions = new(
            new AllowAllExecutionPolicy(),
            new AllowAllProcessEnvironmentPolicy());

        FluNetProcessSessionOutput started = await sessions.StartAsync(
            new FluNetProcessRequest("dotnet", ["--version"]));
        FluNetProcessResult stopped = await sessions.StopAsync(started.SessionId);

        Assert.That(started.SessionId, Is.Not.Empty);
        Assert.That(stopped.TimedOut, Is.False);
    }

    [Test]
    public void ProcessCapabilityAdvertisesSessions()
    {
        ProcessExecutionCapabilityProvider provider = new(
            new DenyFluNetProcessRunner(),
            new PhysicalFluNetProcessSessionRegistry(
                new AllowAllExecutionPolicy(),
                new AllowAllProcessEnvironmentPolicy()));

        Assert.That(provider.IsAvailable, Is.True);
        Assert.That(provider.Descriptor.Permissions, Does.Contain("process.session"));
    }
}
