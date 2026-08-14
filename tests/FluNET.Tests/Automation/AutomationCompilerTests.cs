using FluNET.Automation;
using FluNET.Context;

namespace FluNET.Tests.Automation;

[TestFixture]
public sealed class AutomationCompilerTests
{
    [Test]
    public void EveryCompilesToIntervalTriggerAndNormalWorkflowPlan()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        AutomationCompilationResult result = context.CompileAutomations("""
EVERY 1h
    GET https://api.example.test/posts AS posts
    SAY "Loaded {posts}"
""");
        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True, Diagnostics(result));
            Assert.That(result.Automations, Has.Count.EqualTo(1));
            Assert.That(result.Automations[0].Trigger, Is.EqualTo(new IntervalTriggerDefinition(TimeSpan.FromHours(1))));
            Assert.That(result.Automations[0].Template.Compilation.Plan, Is.Not.Null);
        });
    }

    [Test]
    public void WatchWhenCompilesEventMetadataWithoutSchedulingIt()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        AutomationCompilationResult result = context.CompileAutomations("""
WATCH github.issues IN ppotepa/FluNET
    WHEN opened
        SAY "issue opened"
""");
        Assert.That(result.IsValid, Is.True, Diagnostics(result));
        WatchTriggerDefinition trigger = (WatchTriggerDefinition)result.Automations.Single().Trigger;
        Assert.Multiple(() =>
        {
            Assert.That(trigger.Resource, Is.EqualTo("github.issues IN ppotepa/FluNET"));
            Assert.That(trigger.Event, Is.EqualTo("opened"));
        });
    }

    private static string Diagnostics(AutomationCompilationResult result) => string.Join(" | ", result.Diagnostics.Select(item => $"{item.Code}: {item.Message}"));
}
