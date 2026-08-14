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
}
