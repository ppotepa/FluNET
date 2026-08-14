using FluNET.Declarative.Reconciliation;
using FluNET.Execution.Planning;
using FluNET.Variables;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

namespace FluNET.Context;

public static class ReconciliationExecutionExtensions
{
    private static readonly ConditionalWeakTable<FluNETContext, IReconciliationStateStore> DefaultStateStores = new();
    private static readonly ConditionalWeakTable<FluNETContext, IReconciliationLeaseStore> DefaultLeaseStores = new();
    private static readonly ConditionalWeakTable<FluNETContext, IReconciliationLeaseContextAccessor> DefaultLeaseAccessors = new();
    private static readonly ConditionalWeakTable<FluNETContext, IReconciliationCheckpointStore> DefaultCheckpointStores = new();

    public static IReconciliationMutatorRegistry GetReconciliationMutatorRegistry(this FluNETContext context)
    {
        IReconciliationMutator[] custom = context.ServiceProvider.GetServices<IReconciliationMutator>().ToArray();
        return new ReconciliationMutatorRegistry(custom.Append(new LocalJsonFileReconciliationMutator(context.GetSurfaceCompiler(), context.GetService<IVariableResolver>())));
    }
    public static ReconciliationMutationPlanner GetReconciliationMutationPlanner(this FluNETContext context) => new(context.GetReconciliationMutatorRegistry());
    public static IReconciliationStateStore GetReconciliationStateStore(this FluNETContext context) => context.ServiceProvider.GetService<IReconciliationStateStore>() ?? DefaultStateStores.GetValue(context, _ => new InMemoryReconciliationStateStore());
    public static IReconciliationLeaseStore GetReconciliationLeaseStore(this FluNETContext context) => context.ServiceProvider.GetService<IReconciliationLeaseStore>() ?? DefaultLeaseStores.GetValue(context, _ => new InMemoryReconciliationLeaseStore());
    public static IReconciliationLeaseContextAccessor GetReconciliationLeaseContextAccessor(this FluNETContext context) => context.ServiceProvider.GetService<IReconciliationLeaseContextAccessor>() ?? DefaultLeaseAccessors.GetValue(context, _ => new ReconciliationLeaseContextAccessor());
    public static IReconciliationCheckpointStore GetReconciliationCheckpointStore(this FluNETContext context) => context.ServiceProvider.GetService<IReconciliationCheckpointStore>() ?? DefaultCheckpointStores.GetValue(context, _ => new InMemoryReconciliationCheckpointStore());

    public static ReconciliationRunner GetReconciliationRunner(this FluNETContext context) => new(
        context.GetResourceObserverRegistry(),
        new ReconciliationDiffEngine(),
        context.GetReconciliationMutationPlanner(),
        context.GetService<ExecutionPlanExecutor>(),
        context.GetReconciliationStateStore(),
        context.GetReconciliationCheckpointStore(),
        context.GetReconciliationLeaseContextAccessor());

    public static ReconciliationCoordinator GetReconciliationCoordinator(this FluNETContext context)
    {
        ReconciliationCoordinationOptions options = context.ServiceProvider.GetService<ReconciliationCoordinationOptions>() ?? ReconciliationCoordinationOptions.Default;
        return new(context.GetReconciliationRunner(), context.GetReconciliationLeaseStore(), context.GetReconciliationLeaseContextAccessor(), options);
    }

    public static async ValueTask<IReadOnlyList<ReconciliationRunResult>> ExecuteSyncAsync(this FluNETContext context, string source, CancellationToken cancellationToken = default)
    {
        SyncCompilationResult compilation = context.CompileSync(source);
        if (!compilation.IsValid) throw new InvalidOperationException("SYNC source does not compile: " + string.Join(" | ", compilation.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        IReconciliationExecutor executor = context.GetReconciliationCoordinator();
        List<ReconciliationRunResult> runs = [];
        foreach (SyncDefinition definition in compilation.Definitions) runs.Add(await executor.RunAsync(definition, null, cancellationToken).ConfigureAwait(false));
        return runs;
    }
}
