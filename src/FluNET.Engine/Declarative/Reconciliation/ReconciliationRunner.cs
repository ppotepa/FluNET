using FluNET.Compilation;
using FluNET.Execution.Planning;
using FluNET.Language.Resources;
using FluNET.Prompt.Surface;
using FluNET.Variables;
using System.Security.Cryptography;
using System.Text;

namespace FluNET.Declarative.Reconciliation;

public sealed record ReconciliationMutationPlan(
    SurfaceCompilationResult Compilation,
    string PayloadVariable,
    string Payload)
{
    public bool IsValid => Compilation.IsValid && Compilation.Plan is not null;
}

/// <summary>
/// Synthesizes mutations through the normal compact compiler. The first built-in reconciliation
/// target is a local JSON file; other target kinds must provide an explicit mutation contract.
/// </summary>
public sealed class ReconciliationMutationPlanner(
    SurfaceCompiler compiler,
    IVariableResolver variables)
{
    public ReconciliationMutationPlan Plan(
        SyncDefinition definition,
        DesiredStateSnapshot desired,
        ReconciliationDiff diff)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(desired);
        ArgumentNullException.ThrowIfNull(diff);
        if (diff.HasConflicts)
            throw new ReconciliationConflictException(diff);
        if (!diff.HasMutations)
            throw new InvalidOperationException("A reconciliation mutation plan requires at least one create/update/delete change.");
        if (definition.TargetDescriptor.Reference is not FileResourceReference file ||
            file.IsPattern ||
            definition.TargetDescriptor.Format != ResourceFormat.Json)
        {
            throw new ReconciliationMutationNotSupportedException(
                $"SYNC mutation currently supports a single local JSON target; '{definition.Goal.TargetResource}' is not supported.");
        }
        if (UnsafeSurfaceTarget(file.Path))
            throw new ReconciliationMutationNotSupportedException(
                "The local target path contains compact-syntax separators; use a host mutation provider for this target.");

        string payload = "[" + string.Join(",", desired.Records.Select(record =>
            Encoding.UTF8.GetString(StateCanonicalizer.CanonicalBytes(record.Value)))) + "]";
        string variable = "__reconcile_payload_" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(
                definition.Goal.TargetResource + "|" + definition.Goal.SourceResource)))
            .ToLowerInvariant()[..12];
        variables.Register(variable, payload);

        string source = $"SAVE {variable} TO {file.Path}";
        SurfaceCompilationResult compilation = compiler.Compile(
            new SourceDocument(source, SourceSyntaxKind.Compact));
        if (!compilation.IsValid || compilation.Plan is null)
            throw new InvalidOperationException("Synthesized reconciliation SAVE plan did not compile.");
        return new(compilation, variable, payload);
    }

    private static bool UnsafeSurfaceTarget(string path) =>
        path.Contains(';') || path.Contains('\n') || path.Contains('\r') ||
        path.Contains(" AS ", StringComparison.OrdinalIgnoreCase);
}

public sealed class ReconciliationMutationNotSupportedException(string message)
    : NotSupportedException(message);

public sealed class ReconciliationConflictException : InvalidOperationException
{
    public ReconciliationConflictException(ReconciliationDiff diff)
        : base($"Reconciliation contains {diff.Conflicts} conflict(s); no mutation was applied.") => Diff = diff;
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

/// <summary>
/// Observes source/target concurrently, computes a keyed diff, and delegates any mutation
/// to the ordinary surface compiler + ExecutionPlanExecutor.
/// </summary>
public sealed class ReconciliationRunner(
    IResourceObserverRegistry observers,
    ReconciliationDiffEngine diffEngine,
    ReconciliationMutationPlanner mutationPlanner,
    ExecutionPlanExecutor executor)
{
    public async ValueTask<ReconciliationRunResult> RunAsync(
        SyncDefinition definition,
        ResourceStateSnapshot? baseline = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        List<ExecutionStepResult> mutationSteps = [];
        DesiredStateSnapshot? desired = null;
        ObservedStateSnapshot? observed = null;
        ReconciliationDiff? diff = null;
        ReconciliationMutationPlan? mutation = null;

        if (!definition.IsValid)
            return new(definition, null, null, null, null, mutationSteps, false,
                new InvalidOperationException("SYNC definition has an invalid read compilation."));

        try
        {
            Task<ObservedStateSnapshot> targetTask = ObserveTargetAsync(definition, cancellationToken).AsTask();
            Task<ObservedStateSnapshot> sourceTask = observers.ObserveAsync(
                new ResourceObservationRequest(
                    definition.Goal.SourceResource,
                    definition.Goal.KeyField,
                    ResourceIdentity.Parse(definition.Goal.SourceResource)),
                cancellationToken).AsTask();
            await Task.WhenAll(targetTask, sourceTask).ConfigureAwait(false);

            observed = await targetTask.ConfigureAwait(false);
            ObservedStateSnapshot source = await sourceTask.ConfigureAwait(false);
            desired = new DesiredStateSnapshot(
                source.Identity,
                definition.Goal.KeyField,
                source.Records.Select(record => record.Value),
                source.CapturedAt);
            diff = diffEngine.Compare(desired, observed, baseline);

            if (diff.HasConflicts)
                return new(definition, desired, observed, diff, null, mutationSteps, false,
                    new ReconciliationConflictException(diff));
            if (!diff.HasMutations)
                return new(definition, desired, observed, diff, null, mutationSteps, false, null);

            mutation = mutationPlanner.Plan(definition, desired, diff);
            await executor.ExecuteAsync(
                mutation.Compilation.Plan!,
                mutationSteps,
                cancellationToken).ConfigureAwait(false);
            return new(definition, desired, observed, diff, mutation, mutationSteps, true, null);
        }
        catch (Exception exception)
        {
            return new(definition, desired, observed, diff, mutation, mutationSteps, false, exception);
        }
    }

    private async ValueTask<ObservedStateSnapshot> ObserveTargetAsync(
        SyncDefinition definition,
        CancellationToken cancellationToken)
    {
        try
        {
            return await observers.ObserveAsync(
                new ResourceObservationRequest(
                    definition.Goal.TargetResource,
                    definition.Goal.KeyField,
                    ResourceIdentity.Parse(definition.Goal.TargetResource)),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            definition.TargetDescriptor.Reference is FileResourceReference &&
            exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new ObservedStateSnapshot(
                ResourceIdentity.Parse(definition.Goal.TargetResource),
                definition.Goal.KeyField,
                Array.Empty<System.Text.Json.JsonElement>());
        }
    }
}
