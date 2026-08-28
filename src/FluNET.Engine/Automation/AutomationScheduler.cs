using FluNET.Execution.Planning;
using FluNET.Execution.Workflow;
using System.Collections.Concurrent;
using System.Text;

namespace FluNET.Automation;

public sealed record AutomationScheduleState(
    string AutomationId,
    DateTimeOffset? NextDue,
    string? TriggerIdentity = null);

public sealed record AutomationSchedulerOptions(
    bool AllowConcurrentSignals = false);

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
    private readonly ConcurrentDictionary<string, AutomationScheduleState> _states = new(StringComparer.OrdinalIgnoreCase);

    public ValueTask<AutomationScheduleState?> GetAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _states.TryGetValue(id, out AutomationScheduleState? state);
        return ValueTask.FromResult(state);
    }

    public ValueTask SetAsync(
        AutomationScheduleState state,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _states[state.AutomationId] = state;
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

public sealed class AutomationScheduler
{
    private readonly SentenceExecutor _executor;
    private readonly IAutomationScheduleStore _store;
    private readonly AutomationSchedulerOptions _options;
    private readonly ConcurrentDictionary<string, AutomationDefinition> _automations = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AutomationScheduler(
        SentenceExecutor executor,
        IAutomationScheduleStore store,
        AutomationSchedulerOptions? options = null)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options ?? new AutomationSchedulerOptions();
    }

    public async ValueTask RegisterAsync(
        AutomationDefinition definition,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!definition.Template.IsValid)
            throw new InvalidOperationException($"Automation '{definition.Id}' does not have a valid workflow template.");

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _automations[definition.Id] = definition;
            if (definition.Trigger is not IScheduledTrigger scheduled)
                return;

            string triggerIdentity = ScheduleIdentity(scheduled);
            AutomationScheduleState? state = await _store.GetAsync(definition.Id, cancellationToken).ConfigureAwait(false);
            if (state is null || !StringComparer.Ordinal.Equals(state.TriggerIdentity, triggerIdentity))
            {
                await _store.SetAsync(
                    new AutomationScheduleState(
                        definition.Id,
                        scheduled.NextAfter(now),
                        triggerIdentity),
                    cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
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
                         .Where(item => item.Trigger is IScheduledTrigger)
                         .OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                AutomationScheduleState? state = await _store
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

                await _store.SetAsync(
                    new AutomationScheduleState(
                        automation.Id,
                        next,
                        state.TriggerIdentity ?? ScheduleIdentity(scheduled)),
                    cancellationToken).ConfigureAwait(false);
            }

            return runs;
        }
        finally
        {
            _gate.Release();
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

        if (_options.AllowConcurrentSignals)
            return await PublishSignalCoreAsync(signal, cancellationToken).ConfigureAwait(false);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await PublishSignalCoreAsync(signal, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
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

    private async ValueTask<IReadOnlyList<AutomationRunResult>> PublishSignalCoreAsync(
        AutomationSignal signal,
        CancellationToken cancellationToken)
    {
        List<AutomationRunResult> runs = [];
        foreach (AutomationDefinition automation in _automations.Values.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            if (automation.Trigger is not WatchTriggerDefinition watch ||
                !watch.Resource.Equals(signal.Resource, StringComparison.OrdinalIgnoreCase) ||
                !(watch.Event is null || watch.Event.Equals(signal.EventName, StringComparison.OrdinalIgnoreCase)))
                continue;

            runs.Add(await ExecuteAsync(automation, signal, cancellationToken).ConfigureAwait(false));
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
            object? result = await _executor.ExecuteAsync(
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

    private static string ScheduleIdentity(IScheduledTrigger trigger)
    {
        StringBuilder identity = new(trigger.GetType().FullName ?? trigger.GetType().Name);
        DateTimeOffset cursor = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        for (int index = 0; index < 8; index++)
        {
            cursor = trigger.NextAfter(cursor);
            identity.Append('|').Append(cursor.UtcDateTime.Ticks);
        }

        return identity.ToString();
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
