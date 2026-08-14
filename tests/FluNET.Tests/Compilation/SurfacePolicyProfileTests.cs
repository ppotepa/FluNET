using FluNET.Compilation;
using FluNET.Context;
using FluNET.Execution.Workflow;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class SurfacePolicyProfileTests
{
    [Test]
    public void PolicyProfileExpandsIntoExistingExecutionPolicy()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        const string source = """
POLICY resilient
    RETRY 3
    TIMEOUT 10s
    CONTINUE
WITH resilient
    GET https://api.example.test/posts AS posts
""";
        SurfaceCompilationResult result = context.CompileSurface(source);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True, Diagnostics(result));
            Assert.That(result.Plan!.Steps, Has.Count.EqualTo(1));
            Assert.That(result.Plan.Steps[0].Policy.RetryCount, Is.EqualTo(3));
            Assert.That(result.Plan.Steps[0].Policy.Timeout, Is.EqualTo(TimeSpan.FromSeconds(10)));
            Assert.That(result.Plan.Steps[0].Policy.ErrorBehavior, Is.EqualTo(WorkflowErrorBehavior.Continue));
        });
    }

    [Test]
    public void UsingProfileAppliesToSingleResourceCommand()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        const string source = """
POLICY quick
    RETRY 2
GET https://api.example.test/posts USING quick AS posts
""";
        SurfaceCompilationResult result = context.CompileSurface(source);
        Assert.That(result.IsValid, Is.True, Diagnostics(result));
        Assert.That(result.Plan!.Steps[0].Policy.RetryCount, Is.EqualTo(2));
    }

    private static string Diagnostics(SurfaceCompilationResult result) =>
        string.Join(" | ", result.Lowering.Diagnostics.Select(item => $"{item.Code}: {item.Message}")) + " " +
        string.Join(" | ", result.Diagnostics.Select(item => $"{item.Code}: {item.Message}"));
}
