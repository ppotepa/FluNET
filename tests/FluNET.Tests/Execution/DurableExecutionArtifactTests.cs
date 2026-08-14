using FluNET.Capabilities;
using FluNET.Execution.Commands;

namespace FluNET.Tests.Execution;

[TestFixture]
public sealed class DurableExecutionArtifactTests
{
    private string directory = null!;
    [SetUp] public void Setup() => directory = Path.Combine(Path.GetTempPath(), "FluNET_Artifacts_" + Guid.NewGuid().ToString("N"));
    [TearDown] public void Cleanup() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

    [Test]
    public void IdempotencySurvivesStoreRecreation()
    {
        DurableExecutionArtifactsOptions options = new(directory);
        IExecutionPolicy policy = new AllowAllExecutionPolicy();
        new DurableIdempotencyStore(options, policy).Record("order-1", "created");
        DurableIdempotencyStore reopened = new(options, policy);
        Assert.That(reopened.TryGet("order-1", out object? result), Is.True);
        Assert.That(result, Is.EqualTo("created"));
    }

    [Test]
    public void CacheSurvivesStoreRecreationWhileEntryIsValid()
    {
        DurableExecutionArtifactsOptions options = new(directory);
        IExecutionPolicy policy = new AllowAllExecutionPolicy();
        new DurableExecutionResultCache(options, policy).Set("catalog", new[] { "a", "b" }, TimeSpan.FromMinutes(5));
        DurableExecutionResultCache reopened = new(options, policy);
        Assert.That(reopened.TryGet("catalog", out object? result), Is.True);
        Assert.That(result, Is.TypeOf<string[]>());
        Assert.That((string[])result!, Is.EqualTo(new[] { "a", "b" }));
    }
}
