using FluNET.Capabilities;
using FluNET.Execution.Workflow;

namespace FluNET.Tests.Workflow;

[TestFixture]
public sealed class WorkflowHistoryTests
{
    [Test]
    public async Task AuditProjectionHashesRatherThanExposesResultJson()
    {
        Guid runId = Guid.NewGuid();
        InMemoryWorkflowStateStore store = new();
        await store.AppendAsync(new WorkflowEvent(
            runId,
            0,
            WorkflowStepStatus.Succeeded,
            1,
            DateTimeOffset.UtcNow,
            null,
            "{\"secret\":\"value\"}",
            "plan"));
        WorkflowHistoryService history = new(store, new EmptyWorkflowRunCatalog());
        WorkflowRunHistory run = await history.GetAsync(runId);

        Assert.Multiple(() =>
        {
            Assert.That(run.Summary.SucceededSteps, Is.EqualTo(1));
            Assert.That(run.Events.Single().HasResult, Is.True);
            Assert.That(run.Events.Single().ResultHash, Is.Not.Null.And.Not.Empty);
            Assert.That(run.Events.Single().ToString(), Does.Not.Contain("secret"));
        });
    }

    [Test]
    public async Task DurableCatalogListsPersistedRunsAndBuildsSummaries()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "FluNET_History_" + Guid.NewGuid().ToString("N"));
        DurableWorkflowStoreOptions options = new(directory);
        AllowAllExecutionPolicy policy = new();
        DurableWorkflowStateStore store = new(options, policy);
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        try
        {
            await store.AppendAsync(new WorkflowEvent(
                first,
                0,
                WorkflowStepStatus.Succeeded,
                1,
                now.AddMinutes(-1),
                null,
                "{\"value\":1}",
                "plan-a"));
            await store.AppendAsync(new WorkflowEvent(
                second,
                0,
                WorkflowStepStatus.Failed,
                1,
                now,
                "failed",
                null,
                "plan-b"));

            WorkflowHistoryService history = new(
                store,
                new DurableWorkflowRunCatalog(options, policy));
            IReadOnlyList<WorkflowRunSummary> runs = await history.ListAsync();

            Assert.Multiple(() =>
            {
                Assert.That(runs.Select(run => run.RunId), Is.EquivalentTo(new[] { first, second }));
                Assert.That(runs.Single(run => run.RunId == first).SucceededSteps, Is.EqualTo(1));
                Assert.That(runs.Single(run => run.RunId == second).FailedSteps, Is.EqualTo(1));
                Assert.That(runs[0].RunId, Is.EqualTo(second));
            });
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
