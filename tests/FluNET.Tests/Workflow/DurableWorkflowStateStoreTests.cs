using FluNET.Capabilities;
using FluNET.Execution.Workflow;

namespace FluNET.Tests.Workflow;

[TestFixture]
public sealed class DurableWorkflowStateStoreTests
{
    private string _directory = null!;
    [SetUp] public void Setup() => _directory = Path.Combine(Path.GetTempPath(), "FluNET_Durable_" + Guid.NewGuid().ToString("N"));
    [TearDown] public void Cleanup() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }

    [Test]
    public async Task JournalSurvivesStoreRecreation()
    {
        Guid runId = Guid.NewGuid();
        DurableWorkflowStoreOptions options = new(_directory);
        IExecutionPolicy policy = new AllowAllExecutionPolicy();
        DurableWorkflowStateStore first = new(options, policy);
        WorkflowEvent item = new(runId, 0, WorkflowStepStatus.Succeeded, 1, DateTimeOffset.UtcNow, ResultJson: "42", PlanFingerprint: "plan");
        await first.AppendAsync(item);
        DurableWorkflowStateStore second = new(options, policy);
        IReadOnlyList<WorkflowEvent> restored = await second.ReadAsync(runId);
        Assert.That(restored, Is.EqualTo(new[] { item }));
    }

    [Test]
    public async Task CorruptedJournalFailsInsteadOfSilentlyDroppingHistory()
    {
        Guid runId = Guid.NewGuid();
        DurableWorkflowStateStore store = new(new DurableWorkflowStoreOptions(_directory), new AllowAllExecutionPolicy());
        await store.AppendAsync(new WorkflowEvent(runId, 0, WorkflowStepStatus.Running, 1, DateTimeOffset.UtcNow));
        string path = Path.Combine(_directory, $"{runId:N}.journal.jsonl");
        string text = await File.ReadAllTextAsync(path);
        await File.WriteAllTextAsync(path, text.Replace("Running", "Failed", StringComparison.Ordinal));
        Assert.That(async () => await store.ReadAsync(runId), Throws.TypeOf<InvalidDataException>());
    }
}
