using FluNET.Automation;
using FluNET.Capabilities;
using FluNET.Context;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests.Automation;

[TestFixture]
public sealed class AutomationSchedulerPolicyTests
{
    [Test]
    public async Task ReRegistrationRecalculatesDeadlineWhenScheduledTriggerChanges()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        AutomationDefinition hourly = context.CompileAutomations("""
EVERY 1h
    SAY "tick"
""").Automations.Single();
        AutomationDefinition halfHourly = hourly with
        {
            Trigger = new IntervalTriggerDefinition(TimeSpan.FromMinutes(30))
        };
        InMemoryAutomationScheduleStore store = new();
        AutomationScheduler scheduler = new(
            context.GetService<FluNET.Execution.Planning.SentenceExecutor>(),
            store);
        DateTimeOffset start = new(2026, 8, 28, 10, 0, 0, TimeSpan.Zero);

        await scheduler.RegisterAsync(hourly, start);
        AutomationScheduleState? before = await store.GetAsync(hourly.Id);
        await scheduler.RegisterAsync(halfHourly, start);
        AutomationScheduleState? after = await store.GetAsync(hourly.Id);

        Assert.Multiple(() =>
        {
            Assert.That(before!.NextDue, Is.EqualTo(start.AddHours(1)));
            Assert.That(after!.NextDue, Is.EqualTo(start.AddMinutes(30)));
            Assert.That(after.TriggerIdentity, Is.Not.EqualTo(before.TriggerIdentity));
        });
    }

    [Test]
    public async Task ReRegistrationKeepsDeadlineWhenTriggerIsUnchanged()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        AutomationDefinition automation = context.CompileAutomations("""
EVERY 1h
    SAY "tick"
""").Automations.Single();
        InMemoryAutomationScheduleStore store = new();
        AutomationScheduler scheduler = new(
            context.GetService<FluNET.Execution.Planning.SentenceExecutor>(),
            store);
        DateTimeOffset start = new(2026, 8, 28, 10, 0, 0, TimeSpan.Zero);

        await scheduler.RegisterAsync(automation, start);
        await scheduler.RegisterAsync(automation, start.AddMinutes(45));
        AutomationScheduleState? state = await store.GetAsync(automation.Id);

        Assert.That(state!.NextDue, Is.EqualTo(start.AddHours(1)));
    }

    [Test]
    public async Task SignalExecutionIsSerializedByDefault()
    {
        ConcurrencyTrackingOutput output = new();
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(
            services => services.AddSingleton<ITextOutput>(output));
        AutomationDefinition automation = context.CompileAutomations("""
WATCH files
    WHEN CREATED
        SAY "event"
""").Automations.Single();
        AutomationScheduler scheduler = new(
            context.GetService<FluNET.Execution.Planning.SentenceExecutor>(),
            new InMemoryAutomationScheduleStore());
        await scheduler.RegisterAsync(automation, DateTimeOffset.UtcNow);

        Task first = scheduler.PublishSignalAsync("files", "CREATED").AsTask();
        Task second = scheduler.PublishSignalAsync("files", "CREATED").AsTask();
        await Task.WhenAll(first, second);

        Assert.That(output.MaxConcurrency, Is.EqualTo(1));
    }

    private sealed class ConcurrencyTrackingOutput : ITextOutput
    {
        private int active;
        private int maxConcurrency;

        public int MaxConcurrency => Volatile.Read(ref maxConcurrency);

        public async ValueTask WriteLineAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            int current = Interlocked.Increment(ref active);
            while (true)
            {
                int observed = Volatile.Read(ref maxConcurrency);
                if (current <= observed || Interlocked.CompareExchange(ref maxConcurrency, current, observed) == observed)
                    break;
            }

            try
            {
                await Task.Delay(30, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        }
    }
}
