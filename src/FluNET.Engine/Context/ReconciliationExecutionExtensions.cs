using FluNET.Declarative.Reconciliation;
using FluNET.Execution.Planning;
using FluNET.Variables;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

namespace FluNET.Context;

public static class ReconciliationExecutionExtensions
{
    private static readonly ConditionalWeakTable<FluNETContext, IReconciliationStateStore> DefaultStateStores = new();

    public static ReconciliationMutationPlanner GetReconciliationMutationPlanner(this FluNETContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new ReconciliationMutationPlanner(
            context.GetSurfaceCompiler(),
            context.GetService<IVariableResolver>());
    }

    public static IReconciliationStateStore GetReconciliationStateStore(this FluNETContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.ServiceProvider.GetService<IReconciliationStateStore>()
            ?? DefaultStateStores.GetValue(context, _ => new InMemoryReconciliationStateStore());
    }

    public static ReconciliationRunner GetReconciliationRunner(this FluNETContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new ReconciliationRunner(
            context.GetResourceObserverRegistry(),
            new ReconciliationDiffEngine(),
            context.GetReconciliationMutationPlanner(),
            context.GetService<ExecutionPlanExecutor>(),
            context.GetReconciliationStateStore());
    }

    public static async ValueTask<IReadOnlyList<ReconciliationRunResult>> ExecuteSyncAsync(
        this FluNETContext context,
        string source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        SyncCompilationResult compilation = context.CompileSync(source);
        if (!compilation.IsValid)
            throw new InvalidOperationException(
                "SYNC source does not compile: " + string.Join(" | ", compilation.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        ReconciliationRunner runner = context.GetReconciliationRunner();
        List<ReconciliationRunResult> runs = [];
        foreach (SyncDefinition definition in compilation.Definitions)
            runs.Add(await runner.RunAsync(definition, null, cancellationToken).ConfigureAwait(false));
        return runs;
    }
}
