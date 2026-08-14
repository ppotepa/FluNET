using FluNET.Execution.Workflow;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Context;

public static class WorkflowHistoryExtensions
{
    public static WorkflowHistoryService GetWorkflowHistory(this FluNETContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        IWorkflowRunCatalog catalog = context.ServiceProvider.GetService<IWorkflowRunCatalog>()
            ?? new EmptyWorkflowRunCatalog();
        return new WorkflowHistoryService(
            context.GetService<IWorkflowStateStore>(),
            catalog);
    }
}
