using FluNET.Execution.Planning;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Prompt;

namespace FluNET.Tests.Execution;

[TestFixture]
public sealed class DagPlanningTests
{
    [Test]
    public void AndCreatesAParallelStageAndThenCreatesABarrier()
    {
        LanguageSnapshot language = StandardLanguage.CreateSnapshot();
        ProcessedPrompt prompt = new(
            "SAY first AND SAY second THEN SAY third",
            language.Grammar);
        IReadOnlyList<BoundCommand> commands = new SemanticCommandBinder(language)
            .BindProgram(prompt.Syntax);

        ExecutionPlan plan = new ExecutionPlanner().Create(commands, prompt.Syntax);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Steps[0].Dependencies, Is.Empty);
            Assert.That(plan.Steps[1].Dependencies, Is.Empty);
            Assert.That(plan.Steps[2].Dependencies.Select(edge => edge.PredecessorIndex),
                Is.EquivalentTo(new[] { 0, 1 }));
        });
    }

    [Test]
    public void ParallelCommandsAfterThenShareThePreviousBarrier()
    {
        LanguageSnapshot language = StandardLanguage.CreateSnapshot();
        ProcessedPrompt prompt = new(
            "SAY first THEN SAY second AND SAY third",
            language.Grammar);
        IReadOnlyList<BoundCommand> commands = new SemanticCommandBinder(language)
            .BindProgram(prompt.Syntax);

        ExecutionPlan plan = new ExecutionPlanner().Create(commands, prompt.Syntax);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Steps[1].Dependencies.Select(edge => edge.PredecessorIndex),
                Is.EqualTo(new[] { 0 }));
            Assert.That(plan.Steps[2].Dependencies.Select(edge => edge.PredecessorIndex),
                Is.EqualTo(new[] { 0 }));
        });
    }
}
