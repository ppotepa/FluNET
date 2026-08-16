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
        AutomationScheduler scheduler = new(context.GetService<FluNET.Execution.Planning.SentenceExecutor>(), store);
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
        AutomationScheduler scheduler = new(context.GetService<FluNET.Execution.Planning.SentenceExecutor>(), new InMemoryAutomationScheduleStore());
        await scheduler.RegisterAsync(automation, DateTimeOffset.UtcNow);
        Assert.That(await scheduler.PublishSignalAsync("github.issues IN ppotepa/FluNET", "closed"), Is.Empty);
        Assert.That(await scheduler.PublishSignalAsync("github.issues IN ppotepa/FluNET", "opened"), Has.Count.EqualTo(1));
        Assert.That(output.Messages, Is.EqualTo(new[] { "opened" }));
    }

    [Test]
    public async Task WatchRunPreservesSignalMetadata()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        AutomationDefinition automation = context.CompileAutomations("""
WATCH filesystem IN workspace
    WHEN CREATED
        SAY "created"
""").Automations.Single();
        AutomationScheduler scheduler = new(context.GetService<FluNET.Execution.Planning.SentenceExecutor>(), new InMemoryAutomationScheduleStore());
        await scheduler.RegisterAsync(automation, DateTimeOffset.UtcNow);

        AutomationSignal signal = new("filesystem IN workspace", "CREATED", new Dictionary<string, object?> { ["path"] = "./new.txt", ["length"] = 12L });
        AutomationRunResult run = (await scheduler.PublishSignalAsync(signal)).Single();

        Assert.That(run.Signal, Is.SameAs(signal));
        Assert.That(run.Signal!.Data["path"], Is.EqualTo("./new.txt"));
    }

    [Test]
    public async Task WatchWorkflowCanReadEventInputs()
    {
        CapturingOutput output = new();
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(services => services.AddSingleton<ITextOutput>(output));
        AutomationCompilationResult compilation = context.CompileAutomations("""
WATCH filesystem IN workspace
    WHEN CREATED
        SAY "{event.path}:{event.length}"
""");
        Assert.That(compilation.IsValid, Is.True,
            string.Join(" | ", compilation.Diagnostics.Select(d => d.Code + ":" + d.Message)) + " | " +
            string.Join(" | ", compilation.Automations.SelectMany(a => a.Template.Compilation.Diagnostics.Select(d => d.Code + ":" + d.Message))));
        AutomationDefinition automation = compilation.Automations.Single();
        AutomationScheduler scheduler = new(context.GetService<FluNET.Execution.Planning.SentenceExecutor>(), new InMemoryAutomationScheduleStore());
        await scheduler.RegisterAsync(automation, DateTimeOffset.UtcNow);

        IReadOnlyList<AutomationRunResult> runs = await scheduler.PublishSignalAsync(new AutomationSignal(
            "filesystem IN workspace",
            "CREATED",
            new Dictionary<string, object?> { ["path"] = "./new.txt", ["length"] = 12L }));

        Assert.That(runs, Has.Count.EqualTo(1), string.Join(" | ", runs.Select(run => run.Error?.ToString())));
        Assert.That(runs[0].IsSuccess, Is.True, runs[0].Error?.ToString());
        Assert.That(output.Messages, Is.EqualTo(new[] { "./new.txt:12" }));
    }

    [Test]
    public async Task ReplayRunsStoredSignalsInOrderWithOptionalEventFilter()
    {
        CapturingOutput output = new();
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(services => services.AddSingleton<ITextOutput>(output));
        AutomationDefinition automation = context.CompileAutomations("""
WATCH files
    WHEN CREATED
        SAY "{event.path}"
""").Automations.Single();
        AutomationScheduler scheduler = new(context.GetService<FluNET.Execution.Planning.SentenceExecutor>(), new InMemoryAutomationScheduleStore());
        await scheduler.RegisterAsync(automation, DateTimeOffset.UtcNow);
        InMemoryAutomationSignalStore store = new();
        await store.AppendAsync(new AutomationSignalEnvelope(DateTimeOffset.UtcNow, new AutomationSignal("files", "CREATED", new Dictionary<string, object?> { ["path"] = "a" })));
        await store.AppendAsync(new AutomationSignalEnvelope(DateTimeOffset.UtcNow, new AutomationSignal("files", "CHANGED", new Dictionary<string, object?> { ["path"] = "ignored" })));
        await store.AppendAsync(new AutomationSignalEnvelope(DateTimeOffset.UtcNow, new AutomationSignal("files", "CREATED", new Dictionary<string, object?> { ["path"] = "b" })));

        IReadOnlyList<AutomationRunResult> runs = await scheduler.ReplaySignalsAsync(store, "CREATED");

        Assert.That(runs, Has.Count.EqualTo(2));
        Assert.That(output.Messages, Is.EqualTo(new[] { "a", "b" }));
    }

    private sealed class CapturingOutput : ITextOutput
    {
        public List<string> Messages { get; } = [];
        public ValueTask WriteLineAsync(string message, CancellationToken cancellationToken = default) { Messages.Add(message); return ValueTask.CompletedTask; }
    }
}

