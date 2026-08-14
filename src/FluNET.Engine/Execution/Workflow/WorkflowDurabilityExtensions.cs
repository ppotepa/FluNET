using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Execution.Workflow;

public static class WorkflowDurabilityExtensions
{
    /// <summary>Replaces the default in-memory journal with the durable checksummed single-host store.</summary>
    public static IServiceCollection AddDurableFluNetWorkflows(this IServiceCollection services, string directory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        services.AddSingleton(new DurableWorkflowStoreOptions(directory));
        services.AddSingleton<IWorkflowStateStore, DurableWorkflowStateStore>();
        return services;
    }
}
