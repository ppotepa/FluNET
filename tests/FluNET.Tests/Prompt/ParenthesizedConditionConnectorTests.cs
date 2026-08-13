using FluNET.Context;
using FluNET.Execution;
using FluNET.Prompt;

namespace FluNET.Tests.Prompt;

[TestFixture]
public sealed class ParenthesizedConditionConnectorTests
{
    [Test]
    public async Task AndInsideParenthesizedConditionDoesNotSplitCommand()
    {
        ProcessedPrompt prompt = new("SAY should-not-run IF (true AND false).");
        Assert.That(prompt.Syntax.Commands, Has.Count.EqualTo(1));

        using FluNETContext context = FluNETContext.Create();
        ExecutionResult result = await context.GetEngine().ExecuteAsync(prompt);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
            Assert.That(result.Result, Is.Null);
        });
    }

    [Test]
    public void TopLevelAndRemainsParallelCommandConnector()
    {
        ProcessedPrompt prompt = new("SAY first AND SAY second.");

        Assert.Multiple(() =>
        {
            Assert.That(prompt.Syntax.Commands, Has.Count.EqualTo(2));
            Assert.That(prompt.Syntax.Links, Has.Count.EqualTo(1));
            Assert.That(prompt.Syntax.Links[0].Kind, Is.EqualTo(CommandLinkKind.Parallel));
        });
    }
}
