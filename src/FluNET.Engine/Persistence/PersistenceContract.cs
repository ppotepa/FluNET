using FluNET.Automation;
using FluNET.Declarative;
using FluNET.Execution.Commands;
using FluNET.Execution.Workflow;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Persistence;

public enum PersistenceFeature
{
    WorkflowJournal,
    AutomationSchedule,
    ExecutionCache,
    Idempotency,
    EnsureVersions,
    WorkflowRunCatalog
}

public enum PersistenceDurability
{
    ProcessLocal,
    SingleHostDurable,
    ExternalOrCustom
}

public sealed record PersistenceBinding(
    PersistenceFeature Feature,
    string Contract,
    string? Implementation,
    PersistenceDurability Durability,
    bool IsConfigured);

public sealed record PersistenceContractManifest(
    IReadOnlyList<PersistenceBinding> Bindings)
{
    public PersistenceBinding this[PersistenceFeature feature] =>
        Bindings.Single(binding => binding.Feature == feature);

    public bool IsDurable(PersistenceFeature feature) =>
        this[feature].IsConfigured && this[feature].Durability != PersistenceDurability.ProcessLocal;
}

/// <summary>Describes configured persistence without coupling the independent store contracts together.</summary>
public static class PersistenceContractInspector
{
    public static PersistenceContractManifest Inspect(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        List<PersistenceBinding> bindings =
        [
            Describe(PersistenceFeature.WorkflowJournal, typeof(IWorkflowStateStore), services.GetService<IWorkflowStateStore>()),
            Describe(PersistenceFeature.AutomationSchedule, typeof(IAutomationScheduleStore), services.GetService<IAutomationScheduleStore>()),
            Describe(PersistenceFeature.ExecutionCache, typeof(IExecutionResultCache), services.GetService<IExecutionResultCache>()),
            Describe(PersistenceFeature.Idempotency, typeof(IIdempotencyStore), services.GetService<IIdempotencyStore>()),
            Describe(PersistenceFeature.EnsureVersions, typeof(IEnsureVersionStore), services.GetService<IEnsureVersionStore>()),
            Describe(PersistenceFeature.WorkflowRunCatalog, typeof(IWorkflowRunCatalog), services.GetService<IWorkflowRunCatalog>())
        ];
        return new(bindings);
    }

    private static PersistenceBinding Describe(PersistenceFeature feature, Type contract, object? implementation)
    {
        if (implementation is null)
            return new(feature, Name(contract), null, PersistenceDurability.ProcessLocal, false);

        PersistenceDurability durability = implementation switch
        {
            DurableWorkflowStateStore => PersistenceDurability.SingleHostDurable,
            JsonFileWorkflowStateStore => PersistenceDurability.SingleHostDurable,
            DurableAutomationScheduleStore => PersistenceDurability.SingleHostDurable,
            DurableExecutionResultCache => PersistenceDurability.SingleHostDurable,
            DurableIdempotencyStore => PersistenceDurability.SingleHostDurable,
            DirectoryEnsureVersionStore => PersistenceDurability.SingleHostDurable,
            DurableWorkflowRunCatalog => PersistenceDurability.SingleHostDurable,
            InMemoryWorkflowStateStore => PersistenceDurability.ProcessLocal,
            InMemoryAutomationScheduleStore => PersistenceDurability.ProcessLocal,
            InMemoryExecutionResultCache => PersistenceDurability.ProcessLocal,
            InMemoryIdempotencyStore => PersistenceDurability.ProcessLocal,
            InMemoryEnsureVersionStore => PersistenceDurability.ProcessLocal,
            EmptyWorkflowRunCatalog => PersistenceDurability.ProcessLocal,
            _ => PersistenceDurability.ExternalOrCustom
        };

        return new(feature, Name(contract), Name(implementation.GetType()), durability, true);
    }

    private static string Name(Type type) => type.FullName ?? type.Name;
}
