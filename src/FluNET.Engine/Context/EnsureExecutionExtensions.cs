using FluNET.Declarative;
using FluNET.Execution.Planning;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Context;

public static class EnsureExecutionExtensions
{
    public static EnsureRunner GetEnsureRunner(this FluNETContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        IServiceProvider services = context.ServiceProvider;
        IEnsureVersionStore versions = services.GetService<IEnsureVersionStore>()
            ?? new InMemoryEnsureVersionStore();
        IDesiredStateNotifier notifier = services.GetService<IDesiredStateNotifier>()
            ?? new TextOutputDesiredStateNotifier(
                services.GetRequiredService<FluNET.Capabilities.ITextOutput>());
        return new EnsureRunner(
            services.GetRequiredService<ExecutionPlanExecutor>(),
            services.GetRequiredService<FluNET.Capabilities.IFluNetFileSystem>(),
            versions,
            notifier);
    }

    public static async ValueTask<IReadOnlyList<EnsureRunResult>> ExecuteEnsureAsync(
        this FluNETContext context,
        string source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        DesiredStateCompilationResult compilation = context.CompileEnsure(source);
        if (!compilation.IsValid) return Array.Empty<EnsureRunResult>();
        EnsureRunner runner = context.GetEnsureRunner();
        List<EnsureRunResult> results = [];
        foreach (DesiredStatePlan plan in compilation.Plans)
        {
            results.Add(await runner.RunAsync(plan, cancellationToken).ConfigureAwait(false));
        }
        return results;
    }
}
