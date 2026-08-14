using FluNET.Context;
using FluNET.Declarative;

namespace FluNET.Tests.Declarative;

[TestFixture]
public sealed class EnsureCompilerTests
{
    [Test]
    public void EnsureCompilesToOrdinaryGetAndSavePlan()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        DesiredStateCompilationResult result = context.CompileEnsure("ENSURE backup.json CONTAINS https://api.example.test/users");
        DesiredStatePlan plan = result.Plans.Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True, Diagnostics(result));
            Assert.That(plan.Compilation.Plan!.Steps.Select(step => step.Command.Frame.Id.Value),
                Is.EqualTo(new[] { "surface.get.http.json", "core.save.text" }));
            Assert.That(plan.Compilation.Plan.Steps[1].Dependencies.Select(item => item.PredecessorIndex), Does.Contain(0));
        });
    }

    [Test]
    public void RefreshCompilesToSchedulerReadyAutomationMetadata()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        DesiredStateCompilationResult result = context.CompileEnsure("""
ENSURE backup.json CONTAINS https://api.example.test/users
REFRESH EVERY 1h
KEEP 7 VERSIONS
NOTIFY ON FAILURE
""");
        DesiredStatePlan plan = result.Plans.Single();
        Assert.Multiple(() =>
        {
            Assert.That(plan.Goal.KeepVersions, Is.EqualTo(7));
            Assert.That(plan.Goal.NotifyOnFailure, Is.True);
            Assert.That(plan.RefreshAutomation, Is.Not.Null);
            Assert.That(((FluNET.Automation.IntervalTriggerDefinition)plan.RefreshAutomation!.Trigger).Interval, Is.EqualTo(TimeSpan.FromHours(1)));
        });
    }

    private static string Diagnostics(DesiredStateCompilationResult result) =>
        string.Join(" | ", result.Diagnostics.Select(item => $"{item.Code}: {item.Message}")) + " " +
        string.Join(" | ", result.Plans.SelectMany(plan => plan.Compilation.Diagnostics).Select(item => $"{item.Code}: {item.Message}"));
}
