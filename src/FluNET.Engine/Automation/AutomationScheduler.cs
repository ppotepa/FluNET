using FluNET.Execution.Planning;
using FluNET.Execution.Workflow;
using System.Collections.Concurrent;

namespace FluNET.Automation;

public sealed record AutomationScheduleState(string AutomationId, DateTimeOffset? NextDue);

public interface IAutomationScheduleStore
{
    ValueTask<AutomationScheduleState?> GetAsync(
        string automationId,
        CancellationToken cancellationToken = default);

    ValueTask SetAsync(
        AutomationScheduleState state,
        CancellationToken cancellationToken = default);
}

public sealed class InMemoryAutomationScheduleStore : IAutomationScheduleStore
{
    private readonly ConcurrentDictionary<string, AutomationScheduleState> states = new(StringComparer.OrdinalIgnoreCase);

    public ValueTask<AutomationScheduleState?> GetAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        states.TryGetValue(id, out AutomationScheduleState? state);
        return ValueTask.FromResult(state);
    }

    public ValueTask SetAsync(
        AutomationScheduleState state,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        states[state.AutomationId] = state;
        return ValueTask.CompletedTask;
    }
}

public sealed record AutomationRunResult(
    AutomationDefinition Automation,
    IReadOnlyList<ExecutionStepResult> Steps,
    object? Result,
    Exception? Error,
    AutomationSignal? Signal = null)
{
    public bool IsSuccess => Error is null;
}

public sealed class AutomationScheduler(SentenceExecutor executor, IAutomationScheduleStore store)
{
    private readonly ConcurrentDictionary<string, AutomationDefinition> automations = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim gate = new(1, 1);

    public async ValueTask RegisterAsync(
        AutomationDefinition definition,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (!definition.Template.IsValid)
            throw new InvalidOperationException($"Automation '{definition.Id}' does not have a valid workflow template.");

        automations[definition.Id] = definition;
        if (definition.Trigger is IScheduledTrigger scheduled &&
            await store.GetAsync(definition.Id, cancellationToken).ConfigureAwait(false) is null)
        {
            await store.SetAsync(
                new AutomationScheduleState(definition.Id, scheduled.NextAfter(now)),
                cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask<IReadOnlyList<AutomationRunResult>> TickAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<AutomationRunResult> runs = [];
            foreach (AutomationDefinition automation in automations.Values
                         .Where(item => item.Trigger is IScheduledTrigger)
                         .OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                AutomationScheduleState? state = await store
                    .GetAsync(automation.Id, cancellationToken)
                    .ConfigureAwait(false);
                if (state?.NextDue is not DateTimeOffset due || due > now)
                    continue;

                runs.Add(await ExecuteAsync(automation, null, cancellationToken).ConfigureAwait(false));
                IScheduledTrigger scheduled = (IScheduledTrigger)automation.Trigger;
                DateTimeOffset next = due;
                do
                {
                    next = scheduled.NextAfter(next);
                }
                while (next <= now);

                await store.SetAsync(
                    new AutomationScheduleState(automation.Id, next),
                    cancellationToken).ConfigureAwait(false);
            }

            return runs;
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<IReadOnlyList<AutomationRunResult>> PublishSignalAsync(
        string resource,
        string? eventName = null,
        CancellationToken cancellationToken = default)
        => await PublishSignalAsync(
            new AutomationSignal(resource, eventName, new Dictionary<string, object?>()),
            cancellationToken).ConfigureAwait(false);

    public async ValueTask<IReadOnlyList<AutomationRunResult>> PublishSignalAsync(
        AutomationSignal signal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signal);
        ArgumentException.ThrowIfNullOrWhiteSpace(signal.Resource);
        List<AutomationRunResult> runs = [];
        foreach (AutomationDefinition automation in automations.Values.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            if (automation.Trigger is not WatchTriggerDefinition watch ||
                !watch.Resource.Equals(signal.Resource, StringComparison.OrdinalIgnoreCase) ||
                !(watch.Event is null || watch.Event.Equals(signal.EventName, StringComparison.OrdinalIgnoreCase)))
                continue;

            runs.Add(await ExecuteAsync(automation, signal, cancellationToken).ConfigureAwait(false));
        }

        return runs;
    }

    public async ValueTask<IReadOnlyList<AutomationRunResult>> ReplaySignalsAsync(
        IAutomationSignalStore signalStore,
        string? eventName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signalStore);
        IReadOnlyList<AutomationSignalEnvelope> envelopes = await signalStore
            .ReadAsync(cancellationToken).ConfigureAwait(false);
        List<AutomationRunResult> runs = [];
        foreach (AutomationSignalEnvelope envelope in envelopes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (eventName is not null &&
                !eventName.Equals(envelope.Signal.EventName, StringComparison.OrdinalIgnoreCase))
                continue;
            runs.AddRange(await PublishSignalAsync(envelope.Signal, cancellationToken).ConfigureAwait(false));
        }
        return runs;
    }

    private async ValueTask<AutomationRunResult> ExecuteAsync(
        AutomationDefinition automation,
        AutomationSignal? signal,
        CancellationToken cancellationToken)
    {
        List<ExecutionStepResult> steps = [];
        try
        {
            WorkflowRunState workflow = new();
            if (signal is not null)
            {
                Dictionary<string, object?> eventData = signal.Data.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase);
                eventData["resource"] = signal.Resource;
                eventData["name"] = signal.EventName ?? string.Empty;
                foreach ((string name, object? value) in signal.Data)
                {
                    eventData[name] = NormalizeEventValue(value);
                }
                workflow.SetInput("event", eventData);
            }
            object? result = await executor.ExecuteAsync(
                automation.Template.Compilation.Plan!,
                steps,
                workflow,
                cancellationToken).ConfigureAwait(false);
            return new AutomationRunResult(automation, steps, result, null, signal);
        }
        catch (Exception exception)
        {
            return new AutomationRunResult(automation, steps, null, exception, signal);
        }
    }

    private static object NormalizeEventValue(object? value) => value switch
    {
        null => string.Empty,
        DateTimeOffset timestamp => timestamp.ToString("O"),
        byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal
            => Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture),
        _ => value
    };
}
