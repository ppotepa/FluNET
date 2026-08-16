using FluNET.Compilation;
using FluNET.Execution.Planning;
using FluNET.Language.Resources;

namespace FluNET.Declarative.Reconciliation;

public sealed record ReconciliationMutationPlan(SurfaceCompilationResult Compilation, string PayloadVariable, string Payload)
{
    public bool IsValid => Compilation.IsValid && Compilation.Plan is not null;
    public string? MutatorId { get; init; }
}

public sealed class ReconciliationMutationPlanner
{
    private readonly IReconciliationMutatorRegistry registry;
    public ReconciliationMutationPlanner(SurfaceCompiler compiler, FluNET.Variables.IVariableResolver variables)
        : this(new ReconciliationMutatorRegistry([new LocalJsonFileReconciliationMutator(compiler, variables)])) { }
    public ReconciliationMutationPlanner(IReconciliationMutatorRegistry registry) => this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
    public ReconciliationMutationPlan Plan(SyncDefinition definition, DesiredStateSnapshot desired, ReconciliationDiff diff)
    {
        IReconciliationMutator mutator = registry.Resolve(definition);
        ReconciliationMutationPlan plan = mutator.Plan(new(definition, desired, diff));
        return plan.MutatorId is null ? plan with { MutatorId = mutator.Id } : plan;
    }
}

public sealed class ReconciliationMutationNotSupportedException(string message) : NotSupportedException(message);
public sealed class ReconciliationConflictException : InvalidOperationException
{
    public ReconciliationConflictException(ReconciliationDiff diff) : base($"Reconciliation contains {diff.Conflicts} conflict(s); no mutation was applied.") => Diff = diff;
    public ReconciliationDiff Diff { get; }
}

public sealed record ReconciliationRunResult(
    SyncDefinition Definition,
    DesiredStateSnapshot? Desired,
    ObservedStateSnapshot? Observed,
    ReconciliationDiff? Diff,
    ReconciliationMutationPlan? Mutation,
    IReadOnlyList<ExecutionStepResult> MutationSteps,
    bool Applied,
    Exception? Error)
{
    public bool IsSuccess => Definition.IsValid && Error is null;
}

public sealed class ReconciliationRunner : IReconciliationExecutor
{
    private readonly IResourceObserverRegistry observers;
    private readonly ReconciliationDiffEngine diffEngine;
    private readonly ReconciliationMutationPlanner mutationPlanner;
    private readonly SentenceExecutor executor;
    private readonly IReconciliationStateStore stateStore;
    private readonly IReconciliationCheckpointStore checkpoints;
    private readonly IReconciliationLeaseContextAccessor leaseAccessor;

    public ReconciliationRunner(IResourceObserverRegistry observers, ReconciliationDiffEngine diffEngine, ReconciliationMutationPlanner mutationPlanner, SentenceExecutor executor)
        : this(observers, diffEngine, mutationPlanner, executor, new InMemoryReconciliationStateStore(), new InMemoryReconciliationCheckpointStore(), new ReconciliationLeaseContextAccessor()) { }

    public ReconciliationRunner(
        IResourceObserverRegistry observers,
        ReconciliationDiffEngine diffEngine,
        ReconciliationMutationPlanner mutationPlanner,
        SentenceExecutor executor,
        IReconciliationStateStore stateStore,
        IReconciliationCheckpointStore checkpoints,
        IReconciliationLeaseContextAccessor leaseAccessor)
    {
        this.observers = observers ?? throw new ArgumentNullException(nameof(observers));
        this.diffEngine = diffEngine ?? throw new ArgumentNullException(nameof(diffEngine));
        this.mutationPlanner = mutationPlanner ?? throw new ArgumentNullException(nameof(mutationPlanner));
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        this.stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        this.checkpoints = checkpoints ?? throw new ArgumentNullException(nameof(checkpoints));
        this.leaseAccessor = leaseAccessor ?? throw new ArgumentNullException(nameof(leaseAccessor));
    }

    public async ValueTask<ReconciliationRunResult> RunAsync(SyncDefinition definition, ResourceStateSnapshot? baseline = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Guid runId = Guid.NewGuid();
        List<ExecutionStepResult> mutationSteps = [];
        DesiredStateSnapshot? desired = null;
        ObservedStateSnapshot? observed = null;
        ReconciliationDiff? diff = null;
        ReconciliationMutationPlan? mutation = null;
        if (!definition.IsValid) return new(definition, null, null, null, null, mutationSteps, false, new InvalidOperationException("SYNC definition has an invalid read compilation."));

        try
        {
            IReadOnlyList<ReconciliationCheckpoint> history = await checkpoints.ReadAsync(definition.Id, cancellationToken).ConfigureAwait(false);
            ReconciliationCheckpoint? last = history.LastOrDefault();
            if (last is not null && !last.IsTerminal)
                await CheckpointAsync(runId, definition, ReconciliationCheckpointPhase.Recovering, $"Previous run {last.RunId} stopped at {last.Phase}; re-observing state.", null, cancellationToken).ConfigureAwait(false);
            await CheckpointAsync(runId, definition, ReconciliationCheckpointPhase.Observing, null, null, cancellationToken).ConfigureAwait(false);

            ResourceStateSnapshot? effectiveBaseline = baseline;
            if (effectiveBaseline is null) effectiveBaseline = (await stateStore.GetAsync(definition.Id, cancellationToken).ConfigureAwait(false))?.ToSnapshot();
            Task<ObservedStateSnapshot> targetTask = ObserveTargetAsync(definition, cancellationToken).AsTask();
            Task<ObservedStateSnapshot> sourceTask = observers.ObserveAsync(new ResourceObservationRequest(definition.Goal.SourceResource, definition.Goal.KeyField, ResourceIdentity.Parse(definition.Goal.SourceResource)), cancellationToken).AsTask();
            await Task.WhenAll(targetTask, sourceTask).ConfigureAwait(false);
            observed = await targetTask.ConfigureAwait(false);
            ObservedStateSnapshot source = await sourceTask.ConfigureAwait(false);
            desired = new DesiredStateSnapshot(source.Identity, definition.Goal.KeyField, source.Records.Select(record => record.Value), source.CapturedAt);
            diff = diffEngine.Compare(desired, observed, effectiveBaseline);
            await CheckpointAsync(runId, definition, ReconciliationCheckpointPhase.Diffed, null, diff, cancellationToken).ConfigureAwait(false);

            if (diff.HasConflicts)
            {
                switch (definition.Goal.ConflictPolicy)
                {
                    case ReconciliationConflictPolicy.Fail:
                        ReconciliationConflictException conflict = new(diff);
                        await CheckpointAsync(runId, definition, ReconciliationCheckpointPhase.Failed, conflict.Message, diff, cancellationToken).ConfigureAwait(false);
                        return new(definition, desired, observed, diff, null, mutationSteps, false, conflict);
                    case ReconciliationConflictPolicy.KeepSource: diff = diffEngine.Compare(desired, observed, null); break;
                    case ReconciliationConflictPolicy.KeepTarget: desired = KeepTarget(desired, observed, diff); diff = diffEngine.Compare(desired, observed, null); break;
                    default: throw new InvalidOperationException($"Unknown conflict policy '{definition.Goal.ConflictPolicy}'.");
                }
            }

            if (!diff.HasMutations)
            {
                await SaveBaselineAsync(definition, desired, cancellationToken).ConfigureAwait(false);
                await CheckpointAsync(runId, definition, ReconciliationCheckpointPhase.BaselineSaved, "State already converged.", diff, cancellationToken).ConfigureAwait(false);
                return new(definition, desired, observed, diff, null, mutationSteps, false, null);
            }

            mutation = mutationPlanner.Plan(definition, desired, diff);
            await CheckpointAsync(runId, definition, ReconciliationCheckpointPhase.Applying, mutation.MutatorId, diff, cancellationToken).ConfigureAwait(false);
            await executor.ExecuteAsync(mutation.Compilation.Plan!, mutationSteps, cancellationToken).ConfigureAwait(false);
            await CheckpointAsync(runId, definition, ReconciliationCheckpointPhase.Applied, mutation.MutatorId, diff, cancellationToken).ConfigureAwait(false);
            await SaveBaselineAsync(definition, desired, cancellationToken).ConfigureAwait(false);
            await CheckpointAsync(runId, definition, ReconciliationCheckpointPhase.BaselineSaved, null, diff, cancellationToken).ConfigureAwait(false);
            return new(definition, desired, observed, diff, mutation, mutationSteps, true, null);
        }
        catch (Exception exception)
        {
            try { await CheckpointAsync(runId, definition, ReconciliationCheckpointPhase.Failed, exception.Message, diff, CancellationToken.None).ConfigureAwait(false); } catch { }
            return new(definition, desired, observed, diff, mutation, mutationSteps, false, exception);
        }
    }

    private ValueTask CheckpointAsync(Guid runId, SyncDefinition definition, ReconciliationCheckpointPhase phase, string? message, ReconciliationDiff? diff, CancellationToken cancellationToken) =>
        checkpoints.AppendAsync(new(
            runId,
            definition.Id,
            phase,
            DateTimeOffset.UtcNow,
            leaseAccessor.Current?.FencingToken,
            diff?.Creates,
            diff?.Updates,
            diff?.Deletes,
            diff?.Conflicts,
            message), cancellationToken);

    private static DesiredStateSnapshot KeepTarget(DesiredStateSnapshot desired, ObservedStateSnapshot observed, ReconciliationDiff diff)
    {
        Dictionary<string, System.Text.Json.JsonElement> values = desired.Records.ToDictionary(record => record.Key, record => record.Value.Clone(), StringComparer.Ordinal);
        foreach (ReconciliationChange conflict in diff.Changes.Where(change => change.Kind == ReconciliationChangeKind.Conflict))
        {
            if (conflict.Observed is null) values.Remove(conflict.Key);
            else values[conflict.Key] = conflict.Observed.Value.Clone();
        }
        return new DesiredStateSnapshot(desired.Identity, desired.KeyField, values.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => item.Value), desired.CapturedAt);
    }

    private ValueTask SaveBaselineAsync(SyncDefinition definition, DesiredStateSnapshot converged, CancellationToken cancellationToken) => stateStore.SetAsync(ReconciliationBaselineState.From(definition, converged), cancellationToken);
    private async ValueTask<ObservedStateSnapshot> ObserveTargetAsync(SyncDefinition definition, CancellationToken cancellationToken)
    {
        try { return await observers.ObserveAsync(new ResourceObservationRequest(definition.Goal.TargetResource, definition.Goal.KeyField, ResourceIdentity.Parse(definition.Goal.TargetResource)), cancellationToken).ConfigureAwait(false); }
        catch (Exception exception) when (definition.TargetDescriptor.Reference is FileResourceReference && exception is FileNotFoundException or DirectoryNotFoundException)
        { return new ObservedStateSnapshot(ResourceIdentity.Parse(definition.Goal.TargetResource), definition.Goal.KeyField, Array.Empty<System.Text.Json.JsonElement>()); }
    }
}
