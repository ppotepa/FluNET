using FluNET.Capabilities;
using FluNET.Context;
using FluNET.Execution;
using FluNET.Execution.Planning;
using FluNET.Execution.Workflow;
using FluNET.Prompt;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests.Execution;

[TestFixture]
public sealed class WorkflowRuntimeTests
{
    [Test]
    public async Task RetryEventuallySucceedsAndRecordsEveryAttempt()
    {
        FlakyOutput output = new(failures: 2);
        using FluNETContext context = FluNETContext.Create(services =>
            services.AddSingleton<ITextOutput>(output));

        ExecutionResult result = await context.GetEngine().ExecuteAsync(
            new ProcessedPrompt("SAY stable WITH RETRY {2}."));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
            Assert.That(output.Calls, Is.EqualTo(3));
            Assert.That(result.Steps.Single().Attempts, Is.EqualTo(3));
            Assert.That(result.Workflow!.Events.Count(item =>
                item.Status == WorkflowStepStatus.Retrying), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task TimeoutCanContinueToTheNextStep()
    {
        TimeoutOutput output = new();
        using FluNETContext context = FluNETContext.Create(services =>
            services.AddSingleton<ITextOutput>(output));

        ExecutionResult result = await context.GetEngine().ExecuteAsync(new ProcessedPrompt(
            "SAY slow WITH TIMEOUT {10ms} ON ERROR CONTINUE THEN SAY done."));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
            Assert.That(result.Result, Is.EqualTo("done"));
            Assert.That(result.Steps[0].Status, Is.EqualTo(WorkflowStepStatus.Failed));
            Assert.That(result.Steps[0].Error, Is.TypeOf<WorkflowTimeoutException>());
        });
    }

    [Test]
    public async Task IfElseExecutesExactlyOneAlternative()
    {
        CapturingOutput output = new();
        using FluNETContext context = FluNETContext.Create(services =>
            services.AddSingleton<ITextOutput>(output));

        ExecutionResult result = await context.GetEngine().ExecuteAsync(new ProcessedPrompt(
            "SET BOOLEAN [enabled] TO true " +
            "THEN SAY enabled IF [enabled] ELSE SAY disabled."));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
            Assert.That(output.Messages, Is.EqualTo(new[] { "enabled" }));
            Assert.That(result.Steps.Count(step => step.Status == WorkflowStepStatus.Skipped),
                Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ResumeReusesPersistedSuccessWithoutRepeatingItsEffect()
    {
        CapturingOutput output = new();
        using FluNETContext context = FluNETContext.Create(services =>
            services.AddSingleton<ITextOutput>(output));
        Guid runId = Guid.NewGuid();
        Engine engine = context.GetEngine();

        ExecutionResult first = await engine.ExecuteAsync(
            new ProcessedPrompt("SAY once."),
            new WorkflowExecutionOptions(runId));
        ExecutionResult resumed = await engine.ExecuteAsync(
            new ProcessedPrompt("SAY once."),
            new WorkflowExecutionOptions(runId, Resume: true));

        Assert.Multiple(() =>
        {
            Assert.That(first.IsSuccess, Is.True, first.Error?.Message);
            Assert.That(resumed.IsSuccess, Is.True, resumed.Error?.Message);
            Assert.That(resumed.Result, Is.EqualTo("once"));
            Assert.That(output.Messages, Is.EqualTo(new[] { "once" }));
            Assert.That(resumed.Workflow!.RunId, Is.EqualTo(runId));
        });
    }

    [Test]
    public async Task ResumeRejectsAChangedPlanForTheSameRun()
    {
        CapturingOutput output = new();
        using FluNETContext context = FluNETContext.Create(services =>
            services.AddSingleton<ITextOutput>(output));
        Guid runId = Guid.NewGuid();
        Engine engine = context.GetEngine();

        ExecutionResult first = await engine.ExecuteAsync(
            new ProcessedPrompt("SAY original."),
            new WorkflowExecutionOptions(runId));
        ExecutionResult changed = await engine.ExecuteAsync(
            new ProcessedPrompt("SAY changed."),
            new WorkflowExecutionOptions(runId, Resume: true));

        Assert.Multiple(() =>
        {
            Assert.That(first.IsSuccess, Is.True, first.Error?.Message);
            Assert.That(changed.IsSuccess, Is.False);
            Assert.That(changed.Error?.Code, Is.EqualTo("FLN241"));
            Assert.That(output.Messages, Is.EqualTo(new[] { "original" }));
        });
    }

    [Test]
    public async Task ParallelConditionWaitsForItsVariableProducer()
    {
        CapturingOutput output = new();
        using FluNETContext context = FluNETContext.Create(services =>
            services.AddSingleton<ITextOutput>(output));

        ExecutionResult result = await context.GetEngine().ExecuteAsync(new ProcessedPrompt(
            "SET BOOLEAN [enabled] TO true AND SAY enabled IF [enabled]."));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
            Assert.That(output.Messages, Is.EqualTo(new[] { "enabled" }));
            Assert.That(result.Plan!.Steps[1].Dependencies.Any(dependency =>
                dependency.Kind == ExecutionDependencyKind.Variable), Is.True);
        });
    }

    [Test]
    public async Task JournalFailureAfterAnEffectDoesNotRetryTheEffect()
    {
        CapturingOutput output = new();
        using FluNETContext context = FluNETContext.Create(services =>
        {
            services.AddSingleton<ITextOutput>(output);
            services.AddSingleton<IWorkflowStateStore>(new FailingCompletionStore());
        });

        ExecutionResult result = await context.GetEngine().ExecuteAsync(
            new ProcessedPrompt("SAY once WITH RETRY {3}."));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(output.Messages, Is.EqualTo(new[] { "once" }));
        });
    }

    private sealed class FlakyOutput(int failures) : ITextOutput
    {
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);

        public ValueTask WriteLineAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int attempt = Interlocked.Increment(ref _calls);
            return attempt <= failures
                ? ValueTask.FromException(new IOException("Transient output failure."))
                : ValueTask.CompletedTask;
        }
    }

    private sealed class TimeoutOutput : ITextOutput
    {
        public async ValueTask WriteLineAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            if (message == "slow")
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }

    private sealed class CapturingOutput : ITextOutput
    {
        private readonly List<string> _messages = [];
        public IReadOnlyList<string> Messages => _messages;

        public ValueTask WriteLineAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _messages.Add(message);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FailingCompletionStore : IWorkflowStateStore
    {
        public ValueTask AppendAsync(
            WorkflowEvent item,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return item.Status == WorkflowStepStatus.Succeeded
                ? ValueTask.FromException(new IOException("Workflow journal unavailable."))
                : ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<WorkflowEvent>> ReadAsync(
            Guid runId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IReadOnlyList<WorkflowEvent>>(
                Array.Empty<WorkflowEvent>());
        }
    }
}
