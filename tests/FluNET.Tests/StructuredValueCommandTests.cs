using FluNET.Context;
using FluNET.Execution;
using FluNET.Prompt;
using System.Text.Json;

namespace FluNET.Tests;

[TestFixture]
public sealed class StructuredValueCommandTests
{
    [Test]
    public async Task SetStoresTextForFollowingCommands()
    {
        using FluNETContext context = FluNETContext.Create();

        ExecutionResult result = await context.GetEngine().ExecuteAsync(
            new ProcessedPrompt("SET [message] TO hello world THEN SAY [message]."));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
            Assert.That(result.Result, Is.EqualTo("hello world"));
        });
    }

    [Test]
    public async Task SetAndFormatPreserveAJsonValueAcrossThePlan()
    {
        using FluNETContext context = FluNETContext.Create();

        ExecutionResult result = await context.GetEngine().ExecuteAsync(new ProcessedPrompt(
            "SET JSON [payload] TO {\"name\":\"Ada\"} " +
            "THEN FORMAT JSON [text] FROM [payload] THEN SAY [text]."));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
            Assert.That(result.Result?.ToString(), Does.Contain("Ada"));
            Assert.That(result.Plan!.Variables.Single(variable => variable.Name == "payload").Type.Name,
                Is.EqualTo("Json"));
        });
    }

    [Test]
    public async Task ParseJsonAcceptsTextFromTheHost()
    {
        using FluNETContext context = FluNETContext.Create();
        Engine engine = context.GetEngine();
        engine.RegisterVariable("source", "{\"enabled\":true}");

        ExecutionResult result = await engine.ExecuteAsync(
            new ProcessedPrompt("PARSE JSON [document] FROM [source]."));

        Assert.That(((JsonElement)result.Result!).GetProperty("enabled").GetBoolean(), Is.True);
    }

    [TestCase("SET NUMBER [value] TO 42.", typeof(decimal), "Number")]
    [TestCase("SET BOOLEAN [value] TO true.", typeof(bool), "Boolean")]
    public async Task SetPreservesScalarTypes(string source, Type resultType, string languageType)
    {
        using FluNETContext context = FluNETContext.Create();

        ExecutionResult result = await context.GetEngine().ExecuteAsync(new ProcessedPrompt(source));

        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.TypeOf(resultType));
            Assert.That(result.Plan!.Variables.Single().Type.Name, Is.EqualTo(languageType));
        });
    }
}
