using FluNET.Automation;
using FluNET.Capabilities;
using FluNET.Context;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests.Automation;

[TestFixture]
public sealed class AutomationSchedulerTests
{
    [Test]
    public async Task IntervalTickRunsDueWorkflowExactlyOnceAndAdvancesDeadline()
    {
        CapturingOutput output = new();
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(services => services.AddSingleton<ITextOutput>(output));
        AutomationDefinition automation = context.CompileAutomations("""
EVERY 1h
    SAY "tick"
""").Automations.Single();
        InMemoryAutomationScheduleStore store = new();
        AutomationScheduler scheduler = new(context.GetService<FluNET.Execution.Planning.ExecutionPlanExecutor>(), store);
        DateTimeOffset start = new(2026, 8, 14, 10, 0, 0, TimeSpan.Zero);
        await scheduler.RegisterAsync(automation, start);
        Assert.That(await scheduler.TickAsync(start.AddMinutes(59)), Is.Empty);
        Assert.That(await scheduler.TickAsync(start.AddHours(1)), Has.Count.EqualTo(1));
        Assert.That(await scheduler.TickAsync(start.AddHours(1)), Is.Empty);
        Assert.That(output.Messages, Is.EqualTo(new[] { "tick" }));
    }

    [Test]
    public async Task WatchSignalRunsOnlyMatchingEvent()
    {
        CapturingOutput output = new();
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(services => services.AddSingleton<ITextOutput>(output));
        AutomationDefinition automation = context.CompileAutomations("""
WATCH github.issues IN ppotepa/FluNET
    WHEN opened
        SAY "opened"
""").Automations.Single();
        AutomationScheduler scheduler = new(context.GetService<FluNET.Execution.Planning.ExecutionPlanExecutor>(), new InMemoryAutomationScheduleStore());
        await scheduler.RegisterAsync(automation, DateTimeOffset.UtcNow);
        Assert.That(await scheduler.PublishSignalAsync("github.issues IN ppotepa/FluNET", "closed"), Is.Empty);
        Assert.That(await scheduler.PublishSignalAsync("github.issues IN ppotepa/FluNET", "opened"), Has.Count.EqualTo(1));
        Assert.That(output.Messages, Is.EqualTo(new[] { "opened" }));
    }

    private sealed class CapturingOutput : ITextOutput
    {
        public List<string> Messages { get; } = [];
        public ValueTask WriteLineAsync(string message, CancellationToken cancellationToken = default) { Messages.Add(message); return ValueTask.CompletedTask; }
    }
}
