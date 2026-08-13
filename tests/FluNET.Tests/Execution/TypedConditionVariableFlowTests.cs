using FluNET.Context;
using FluNET.Execution;
using FluNET.Prompt;

namespace FluNET.Tests.Execution;

[TestFixture]
public sealed class TypedConditionVariableFlowTests
{
    [Test]
    public async Task ConditionReadsTypedValueProducedByEarlierStep()
    {
        using FluNETContext context = FluNETContext.Create();

        ExecutionResult result = await context.GetEngine().ExecuteAsync(
            new ProcessedPrompt(
                "SET BOOLEAN [enabled] TO false " +
                "THEN SAY should-not-run IF ([enabled] AND true)."));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
            Assert.That(result.Result, Is.Not.EqualTo("should-not-run"));
        });
    }
}
