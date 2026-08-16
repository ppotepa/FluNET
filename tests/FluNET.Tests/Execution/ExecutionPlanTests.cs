using FluNET.Context;
using FluNET.Execution;
using FluNET.Execution.Planning;
using FluNET.Prompt;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests.Execution;

[TestFixture]
public sealed class ExecutionPlanTests
{
    [Test]
    public void Analyze_ProducesAnImmutablePlanWithExplicitResultBindings()
    {
        using FluNETContext context = FluNETContext.Create();

        PromptAnalysis analysis = context.GetEngine().Analyze(new ProcessedPrompt(
            "GET [content] FROM {input.txt} THEN SAY [content]."));

        Assert.Multiple(() =>
        {
            Assert.That(analysis.IsValid, Is.True, analysis.ValidationResult.FailureReason);
            Assert.That(analysis.Plan, Is.Not.Null);
            Assert.That(analysis.Plan!.Steps, Has.Count.EqualTo(2));
            Assert.That(analysis.Plan.Steps[0].ResultBinding?.Targets,
                Is.EqualTo(new[] { "content" }));
            Assert.That(analysis.Plan.Steps[1].ResultBinding, Is.Null);
            Assert.That(analysis.Plan.Steps[1].Dependencies,
                Has.Some.Matches<ExecutionDependency>(dependency =>
                    dependency.Kind == ExecutionDependencyKind.Sequence &&
                    dependency.PredecessorIndex == 0));
            Assert.That(analysis.Plan.Steps[1].Dependencies,
                Has.Some.Matches<ExecutionDependency>(dependency =>
                    dependency.Kind == ExecutionDependencyKind.Variable &&
                    dependency.Variable == "content"));
        });
    }

    [Test]
    public async Task Execute_ReportsEveryCompletedPlanStep()
    {
        using FluNETContext context = FluNETContext.Create();

        ExecutionResult execution = await context.GetEngine().ExecuteAsync(
            new ProcessedPrompt("SAY first THEN SAY second."));

        Assert.Multiple(() =>
        {
            Assert.That(execution.IsSuccess, Is.True, execution.Error?.Message);
            Assert.That(execution.Plan?.Steps, Has.Count.EqualTo(2));
            Assert.That(execution.Steps.Select(step => step.Result),
                Is.EqualTo(new object?[] { "first", "second" }));
            Assert.That(execution.Result, Is.EqualTo("second"));
        });
    }

    [Test]
    public async Task StandardPipeline_ResolvesSentenceExecutor()
    {
        using FluNETContext context = FluNETContext.Create();

        ExecutionResult execution = await context.GetEngine().ExecuteAsync(
            new ProcessedPrompt("SAY typed."));

        Assert.Multiple(() =>
        {
            Assert.That(execution.IsSuccess, Is.True, execution.Error?.Message);
            Assert.That(context.GetService<FluNET.Execution.Planning.SentenceExecutor>(), Is.Not.Null);
        });
    }

    [Test]
    public async Task InputVariablesAreNotOverwrittenByCommandResults()
    {
        using FluNETContext context = FluNETContext.Create();
        Engine engine = context.GetEngine();
        engine.RegisterVariable("text", "plain");

        ExecutionResult transform = await engine.ExecuteAsync(
            new ProcessedPrompt("TRANSFORM [text] USING UTF8."));
        ExecutionResult readBack = await engine.ExecuteAsync(
            new ProcessedPrompt("SAY [text]."));

        Assert.Multiple(() =>
        {
            Assert.That(transform.IsSuccess, Is.True, transform.Error?.Message);
            Assert.That(readBack.Result, Is.EqualTo("plain"));
        });
    }
}

