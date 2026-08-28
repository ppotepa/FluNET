using FluNET.Capabilities;

namespace FluNET.Tests.Capabilities;

[TestFixture]
public sealed class ProcessExecutionHardeningTests
{
    [Test]
    public void RunnerRejectsNegativeTimeoutBeforeStartingProcess()
    {
        PhysicalFluNetProcessRunner runner = new();

        Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            _ = await runner.RunAsync(new FluNetProcessRequest(
                "dotnet",
                ["--version"],
                Timeout: TimeSpan.FromMilliseconds(-2)));
        });
    }

    [Test]
    public async Task SessionUsesSameEnvironmentNameValidationAsOneShotRunner()
    {
        await using PhysicalFluNetProcessSessionRegistry sessions = new(
            new AllowAllExecutionPolicy(),
            new AllowAllProcessEnvironmentPolicy());

        Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            _ = await sessions.StartAsync(new FluNetProcessRequest(
                "dotnet",
                ["--version"],
                Environment: new Dictionary<string, string>
                {
                    ["INVALID=NAME"] = "value"
                }));
        });
    }
}
