using FluNET.Context;
using FluNET.Prompt;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class TypedAnalysisTests
{
    [Test]
    public void TypedAnalysisRejectsInvalidLiteralWithoutExecutingCommand()
    {
        using FluNETContext context = FluNETContext.Create();

        TypedAnalysisResult result = context.AnalyzeTyped(
            new ProcessedPrompt("SET NUMBER [value] TO banana."));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.TypedProgram, Is.Null);
            Assert.That(result.CompilationError?.Code, Is.EqualTo("FLN140"));
        });
    }

    [Test]
    public void TypedAnalysisReturnsCompiledProgramForValidPrompt()
    {
        using FluNETContext context = FluNETContext.Create();

        TypedAnalysisResult result = context.AnalyzeTyped(
            new ProcessedPrompt("SET NUMBER [value] TO 42."));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True, result.CompilationError?.Message);
            Assert.That(result.TypedProgram, Is.Not.Null);
            Assert.That(result.TypedProgram!.Commands, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void TypedAnalysisAcceptsExplicitImplicitConversionGraphBetweenCommands()
    {
        using FluNETContext context = FluNETContext.Create();

        TypedAnalysisResult result = context.AnalyzeTyped(
            new ProcessedPrompt("SET NUMBER [value] TO 42 THEN SAY [value]."));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True, result.CompilationError?.Message);
            Assert.That(result.TypedProgram, Is.Not.Null);
            Assert.That(result.TypedProgram!.Commands, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void TypedAnalysisAcceptsListTextProducerForTextConsumer()
    {
        using FluNETContext context = FluNETContext.Create();

        TypedAnalysisResult result = context.AnalyzeTyped(
            new ProcessedPrompt("GET [lines] FROM {input.txt} THEN SAY [lines]."));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True, result.CompilationError?.Message);
            Assert.That(result.TypedProgram, Is.Not.Null);
            Assert.That(result.TypedProgram!.Commands, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void TypedAnalysisAcceptsLegacyDestructuredTargetsAsRuntimeTypedOutputs()
    {
        using FluNETContext context = FluNETContext.Create();

        TypedAnalysisResult result = context.AnalyzeTyped(
            new ProcessedPrompt(
                "GET [{firstName, lastName}] FROM {person.json} THEN SAY [firstName]."));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True, result.CompilationError?.Message);
            Assert.That(result.TypedProgram, Is.Not.Null);
            Assert.That(result.TypedProgram!.Commands, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void TypedAnalysisAcceptsRegisteredHostVariableThroughConversionGraph()
    {
        using FluNETContext context = FluNETContext.Create();
        context.GetEngine().RegisterVariable("count", 42m);

        TypedAnalysisResult result = context.AnalyzeTyped(
            new ProcessedPrompt("SAY [count]."));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True, result.CompilationError?.Message);
            Assert.That(result.TypedProgram, Is.Not.Null);
        });
    }

    [Test]
    public void TypedAnalysisRejectsUnresolvedVariableBeforePlanningOrExecution()
    {
        using FluNETContext context = FluNETContext.Create();

        TypedAnalysisResult result = context.AnalyzeTyped(
            new ProcessedPrompt("SAY [missing]."));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.TypedProgram, Is.Null);
            Assert.That(result.CompilationError?.Code, Is.EqualTo("FLN150"));
        });
    }

    [Test]
    public void TypedAnalysisRejectsUnresolvedConditionVariable()
    {
        using FluNETContext context = FluNETContext.Create();

        TypedAnalysisResult result = context.AnalyzeTyped(
            new ProcessedPrompt("SAY guarded IF [missing]."));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.TypedProgram, Is.Null);
            Assert.That(result.CompilationError?.Code, Is.EqualTo("FLN150"));
        });
    }
}
