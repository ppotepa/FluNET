using FluNET.Execution.Planning;
using FluNET.Execution.Workflow;
using System.Collections.Concurrent;

namespace FluNET.Automation;

public sealed record AutomationScheduleState(string AutomationId, DateTimeOffset? NextDue);

public interface IAutomationScheduleStore
{
    ValueTask<AutomationScheduleState?> GetAsync(string automationId, CancellationToken cancellationToken = default);
    ValueTask SetAsync(AutomationScheduleState state, CancellationToken cancellationToken = default);
}

public sealed class InMemoryAutomationScheduleStore : IAutomationScheduleStore
{
    private readonly ConcurrentDictionary<string, AutomationScheduleState> _states = new(StringComparer.OrdinalIgnoreCase);
    public ValueTask<AutomationScheduleState?> GetAsync(string automationId, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); _states.TryGetValue(automationId, out AutomationScheduleState? state); return ValueTask.FromResult(state); }
    public ValueTask SetAsync(AutomationScheduleState state, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); _states[state.AutomationId] = state; return ValueTask.CompletedTask; }
}

public sealed record AutomationRunResult(
    AutomationDefinition Automation,
    IReadOnlyList<ExecutionStepResult> Steps,
    object? Result,
    Exception? Error)
{
    public bool IsSuccess => Error is null;
}

/// <summary>
/// Deterministic host-driven scheduler. It owns no thread/timer; TickAsync and PublishSignalAsync
/// execute already-compiled plans through the canonical ExecutionPlanExecutor.
/// </summary>
public sealed class AutomationScheduler(
    ExecutionPlanExecutor executor,
    IAutomationScheduleStore scheduleStore)
{
    private readonly ConcurrentDictionary<string, AutomationDefinition> _automations = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async ValueTask RegisterAsync(
        AutomationDefinition definition,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!definition.Template.IsValid) throw new InvalidOperationException($"Automation '{definition.Id}' does not have a valid workflow template.");
        _automations[definition.Id] = definition;
        if (definition.Trigger is IntervalTriggerDefinition interval &&
            await scheduleStore.GetAsync(definition.Id, cancellationToken).ConfigureAwait(false) is null)
        {
            await scheduleStore.SetAsync(new AutomationScheduleState(definition.Id, now.Add(interval.Interval)), cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask<IReadOnlyList<AutomationRunResult>> TickAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<AutomationRunResult> runs = [];
            foreach (AutomationDefinition automation in _automations.Values
                .Where(item => item.Trigger is IntervalTriggerDefinition)
                .OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                AutomationScheduleState? state = await scheduleStore.GetAsync(automation.Id, cancellationToken).ConfigureAwait(false);
                if (state?.NextDue is not DateTimeOffset due || due > now) continue;
                runs.Add(await ExecuteAsync(automation, cancellationToken).ConfigureAwait(false));
                TimeSpan interval = ((IntervalTriggerDefinition)automation.Trigger).Interval;
                DateTimeOffset next = due;
                do { next = next.Add(interval); } while (next <= now);
                await scheduleStore.SetAsync(new AutomationScheduleState(automation.Id, next), cancellationToken).ConfigureAwait(false);
            }
            return runs;
        }
        finally { _gate.Release(); }
    }

    public async ValueTask<IReadOnlyList<AutomationRunResult>> PublishSignalAsync(
        string resource,
        string? eventName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        List<AutomationRunResult> runs = [];
        foreach (AutomationDefinition automation in _automations.Values.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            if (automation.Trigger is not WatchTriggerDefinition watch ||
                !watch.Resource.Equals(resource, StringComparison.OrdinalIgnoreCase) ||
                !(watch.Event is null || watch.Event.Equals(eventName, StringComparison.OrdinalIgnoreCase))) continue;
            runs.Add(await ExecuteAsync(automation, cancellationToken).ConfigureAwait(false));
        }
        return runs;
    }

    private async ValueTask<AutomationRunResult> ExecuteAsync(AutomationDefinition automation, CancellationToken cancellationToken)
    {
        List<ExecutionStepResult> steps = [];
        try
        {
            object? result = await executor.ExecuteAsync(
                automation.Template.Compilation.Plan!,
                steps,
                new WorkflowRunState(),
                cancellationToken).ConfigureAwait(false);
            return new AutomationRunResult(automation, steps, result, null);
        }
        catch (Exception exception) { return new AutomationRunResult(automation, steps, null, exception); }
    }
}
