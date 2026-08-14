using FluNET.Declarative.Reconciliation;

namespace FluNET.Context;

public static class ReconciliationWatchExtensions
{
    public static ReconciliationWatchCompilationResult CompileReconciliationWatches(
        this FluNETContext context,
        string source)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new ReconciliationWatchCompiler(context.GetSyncCompiler()).Compile(source);
    }

    public static ReconciliationWatchScheduler GetReconciliationWatchScheduler(this FluNETContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new ReconciliationWatchScheduler(context.GetReconciliationRunner());
    }
}
