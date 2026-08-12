using FluNET.Capabilities;
using FluNET.Context;
using FluNET.Execution;
using FluNET.Prompt;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests.Execution;

[TestFixture]
public sealed class ParallelExecutionTests
{
    [Test]
    public async Task IndependentAndBranchesRunConcurrentlyBeforeThenBarrier()
    {
        ConcurrencyProbeOutput output = new();
        using FluNETContext context = FluNETContext.Create(services =>
            services.AddSingleton<ITextOutput>(output));

        ExecutionResult result = await context.GetEngine().ExecuteAsync(new ProcessedPrompt(
            "SAY first AND SAY second THEN SAY third."));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
            Assert.That(output.MaximumConcurrency, Is.EqualTo(2));
            Assert.That(output.Messages.Last(), Is.EqualTo("third"));
            Assert.That(result.Steps.Select(step => step.Step.Index), Is.EqualTo(new[] { 0, 1, 2 }));
        });
    }

    [Test]
    public void PlannerRejectsParallelWritesToTheSameVariable()
    {
        using FluNETContext context = FluNETContext.Create();

        PromptAnalysis analysis = context.GetEngine().Analyze(new ProcessedPrompt(
            "SET [value] TO first AND SET [value] TO second."));

        Assert.That(analysis.ValidationResult.FailureReason,
            Does.Contain("both write [value]"));
    }

    private sealed class ConcurrencyProbeOutput : ITextOutput
    {
        private readonly object _gate = new();
        private int _active;
        private int _maximum;
        private readonly List<string> _messages = [];

        public int MaximumConcurrency => Volatile.Read(ref _maximum);
        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (_gate)
                {
                    return _messages.ToArray();
                }
            }
        }

        public async ValueTask WriteLineAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            int active = Interlocked.Increment(ref _active);
            int observed;
            do
            {
                observed = Volatile.Read(ref _maximum);
            }
            while (active > observed &&
                Interlocked.CompareExchange(ref _maximum, active, observed) != observed);

            try
            {
                await Task.Delay(50, cancellationToken);
                lock (_gate)
                {
                    _messages.Add(message);
                }
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }
    }
}
