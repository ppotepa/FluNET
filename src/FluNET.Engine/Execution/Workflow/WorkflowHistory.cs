using FluNET.Capabilities;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Text;

namespace FluNET.Execution.Workflow;

public interface IWorkflowRunCatalog
{
    ValueTask<IReadOnlyList<Guid>> ListRunIdsAsync(CancellationToken cancellationToken = default);
}

public sealed class EmptyWorkflowRunCatalog : IWorkflowRunCatalog
{
    public ValueTask<IReadOnlyList<Guid>> ListRunIdsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>());
    }
}

/// <summary>Lists run ids from the existing durable single-host journal directory.</summary>
public sealed class DurableWorkflowRunCatalog(
    DurableWorkflowStoreOptions options,
    IExecutionPolicy policy) : IWorkflowRunCatalog
{
    private readonly string directory = Path.GetFullPath(options.Directory);

    public ValueTask<IReadOnlyList<Guid>> ListRunIdsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string probe = Path.Combine(directory, ".history-access");
        policy.EnsureFileAccess(probe);
        if (!Directory.Exists(directory))
            return ValueTask.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>());

        List<Guid> ids = [];
        foreach (string file in Directory.EnumerateFiles(directory, "*.journal.jsonl"))
        {
            string name = Path.GetFileName(file);
            const string suffix = ".journal.jsonl";
            string id = name[..^suffix.Length];
            if (Guid.TryParseExact(id, "N", out Guid parsed)) ids.Add(parsed);
        }
        ids.Sort();
        return ValueTask.FromResult<IReadOnlyList<Guid>>(ids);
    }
}

public sealed record WorkflowAuditEvent(
    int StepIndex,
    WorkflowStepStatus Status,
    int Attempt,
    DateTimeOffset Timestamp,
    string? Message,
    bool HasResult,
    string? ResultHash,
    string? PlanFingerprint)
{
    internal static WorkflowAuditEvent FromEvent(WorkflowEvent item) => new(
        item.StepIndex,
        item.Status,
        item.Attempt,
        item.Timestamp,
        item.Message,
        item.ResultJson is not null,
        item.ResultJson is null ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(item.ResultJson))).ToLowerInvariant(),
        item.PlanFingerprint);
}

public sealed record WorkflowRunSummary(
    Guid RunId,
    DateTimeOffset? StartedAt,
    DateTimeOffset? LastUpdatedAt,
    WorkflowStepStatus? LastStatus,
    int EventCount,
    int SucceededSteps,
    int FailedSteps,
    string? PlanFingerprint);

public sealed record WorkflowRunHistory(
    WorkflowRunSummary Summary,
    IReadOnlyList<WorkflowAuditEvent> Events);

public sealed class WorkflowHistoryService(
    IWorkflowStateStore stateStore,
    IWorkflowRunCatalog catalog)
{
    public async ValueTask<WorkflowRunHistory> GetAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WorkflowEvent> events = await stateStore.ReadAsync(runId, cancellationToken).ConfigureAwait(false);
        WorkflowEvent[] ordered = events.OrderBy(item => item.Timestamp).ThenBy(item => item.StepIndex).ThenBy(item => item.Attempt).ToArray();
        WorkflowRunSummary summary = Summarize(runId, ordered);
        return new(summary, ordered.Select(WorkflowAuditEvent.FromEvent).ToArray());
    }

    public async ValueTask<IReadOnlyList<WorkflowRunSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Guid> ids = await catalog.ListRunIdsAsync(cancellationToken).ConfigureAwait(false);
        List<WorkflowRunSummary> summaries = [];
        foreach (Guid id in ids)
        {
            IReadOnlyList<WorkflowEvent> events = await stateStore.ReadAsync(id, cancellationToken).ConfigureAwait(false);
            summaries.Add(Summarize(id, events));
        }
        return summaries
            .OrderByDescending(summary => summary.LastUpdatedAt)
            .ThenBy(summary => summary.RunId)
            .ToArray();
    }

    private static WorkflowRunSummary Summarize(Guid runId, IReadOnlyList<WorkflowEvent> events)
    {
        WorkflowEvent? last = events.OrderBy(item => item.Timestamp).LastOrDefault();
        int succeeded = events.Where(item => item.Status == WorkflowStepStatus.Succeeded)
            .Select(item => item.StepIndex).Distinct().Count();
        int failed = events.Where(item => item.Status == WorkflowStepStatus.Failed)
            .Select(item => item.StepIndex).Distinct().Count();
        return new(
            runId,
            events.Count == 0 ? null : events.Min(item => item.Timestamp),
            last?.Timestamp,
            last?.Status,
            events.Count,
            succeeded,
            failed,
            events.Select(item => item.PlanFingerprint).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)));
    }
}

public static class WorkflowHistoryServiceCollectionExtensions
{
    public static IServiceCollection AddDurableFluNetWorkflowHistory(this IServiceCollection services, string directory)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddDurableFluNetWorkflows(directory);
        services.AddSingleton<IWorkflowRunCatalog, DurableWorkflowRunCatalog>();
        services.AddTransient<WorkflowHistoryService>();
        return services;
    }
}
