using FluNET.Compilation;
using FluNET.Language.Resources;
using FluNET.Prompt.Surface;
using FluNET.Variables;
using System.Security.Cryptography;
using System.Text;

namespace FluNET.Declarative.Reconciliation;

public sealed record ReconciliationMutationRequest(
    SyncDefinition Definition,
    DesiredStateSnapshot Desired,
    ReconciliationDiff Diff);

/// <summary>
/// A mutator is a side-effect-free mutation-plan factory. It must return a plan that is later
/// executed by ExecutionPlanExecutor; implementations must not perform the mutation in Plan().
/// </summary>
public interface IReconciliationMutator
{
    string Id { get; }
    int Priority => 0;
    bool CanMutate(SyncDefinition definition);
    ReconciliationMutationPlan Plan(ReconciliationMutationRequest request);
}

public interface IReconciliationMutatorRegistry
{
    IReadOnlyList<IReconciliationMutator> Mutators { get; }
    IReconciliationMutator Resolve(SyncDefinition definition);
}

public sealed class ReconciliationMutatorRegistry : IReconciliationMutatorRegistry
{
    private readonly IReconciliationMutator[] mutators;

    public ReconciliationMutatorRegistry(IEnumerable<IReconciliationMutator> mutators)
    {
        this.mutators = (mutators ?? throw new ArgumentNullException(nameof(mutators)))
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        string[] duplicateIds = this.mutators
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateIds.Length > 0)
            throw new InvalidOperationException($"Reconciliation mutator ids must be unique: {string.Join(", ", duplicateIds)}.");
    }

    public IReadOnlyList<IReconciliationMutator> Mutators => mutators;

    public IReconciliationMutator Resolve(SyncDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        IReconciliationMutator[] matches = mutators.Where(item => item.CanMutate(definition)).ToArray();
        if (matches.Length == 0)
            throw new ReconciliationMutationNotSupportedException(
                $"No reconciliation mutator handles target '{definition.Goal.TargetResource}'.");
        int highest = matches.Max(item => item.Priority);
        IReconciliationMutator[] preferred = matches.Where(item => item.Priority == highest).ToArray();
        if (preferred.Length != 1)
            throw new InvalidOperationException(
                $"Target '{definition.Goal.TargetResource}' matches multiple reconciliation mutators at priority {highest}: " +
                string.Join(", ", preferred.Select(item => item.Id)) + ".");
        return preferred[0];
    }
}

/// <summary>Built-in local JSON replacement mutator. Custom mutators may override it with a higher priority.</summary>
public sealed class LocalJsonFileReconciliationMutator(
    SurfaceCompiler compiler,
    IVariableResolver variables) : IReconciliationMutator
{
    public string Id => "core.reconciliation.local-json";
    public int Priority => -1000;

    public bool CanMutate(SyncDefinition definition) =>
        definition.TargetDescriptor.Reference is FileResourceReference file &&
        !file.IsPattern &&
        definition.TargetDescriptor.Format == ResourceFormat.Json;

    public ReconciliationMutationPlan Plan(ReconciliationMutationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        SyncDefinition definition = request.Definition;
        DesiredStateSnapshot desired = request.Desired;
        ReconciliationDiff diff = request.Diff;
        if (diff.HasConflicts) throw new ReconciliationConflictException(diff);
        if (!diff.HasMutations)
            throw new InvalidOperationException("A reconciliation mutation plan requires at least one create/update/delete change.");
        FileResourceReference file = (FileResourceReference)definition.TargetDescriptor.Reference;
        if (UnsafeSurfaceTarget(file.Path))
            throw new ReconciliationMutationNotSupportedException(
                "The local target path contains compact-syntax separators; use a custom reconciliation mutator for this target.");

        string payload = "[" + string.Join(",", desired.Records.Select(record =>
            Encoding.UTF8.GetString(StateCanonicalizer.CanonicalBytes(record.Value)))) + "]";
        string variable = "__reconcile_payload_" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(definition.Id))).ToLowerInvariant()[..12];
        variables.Register(variable, payload);
        SurfaceCompilationResult compilation = compiler.Compile(
            new SourceDocument($"SAVE {variable} TO {file.Path}", SourceSyntaxKind.Compact));
        if (!compilation.IsValid || compilation.Plan is null)
            throw new InvalidOperationException("Synthesized reconciliation SAVE plan did not compile.");
        return new(compilation, variable, payload) { MutatorId = Id };
    }

    private static bool UnsafeSurfaceTarget(string path) =>
        path.Contains(';') || path.Contains('\n') || path.Contains('\r') ||
        path.Contains(" AS ", StringComparison.OrdinalIgnoreCase);
}
