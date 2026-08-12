using FluNET.Language;
using FluNET.Prompt;
using FluNET.Syntax.Verbs;

namespace FluNET.Tests.Language;

[TestFixture]
public sealed class LanguageGrammarTests
{
    [Test]
    public void SnapshotOwnsClauseMarkersAndCommandConnectors()
    {
        LanguageBuilder builder = new LanguageBuilder()
            .ClauseMarker("VIA")
            .CommandConnector("NEXT", CommandLinkKind.Sequence);
        builder.Command<SayText, string>("SAY", "Text")
            .Positional<string>(SemanticRole.Theme);
        LanguageSnapshot language = builder.Build();

        ProcessedPrompt prompt = new("SAY first NEXT SAY second", language.Grammar);

        Assert.Multiple(() =>
        {
            Assert.That(prompt.IsValid, Is.True);
            Assert.That(prompt.Syntax.Commands, Has.Count.EqualTo(2));
            Assert.That(prompt.Syntax.Links, Has.Count.EqualTo(1));
            Assert.That(prompt.Syntax.Links[0].Kind, Is.EqualTo(CommandLinkKind.Sequence));
            Assert.That(language.Grammar.ClauseMarkers, Does.Contain("VIA"));
        });
    }

    [Test]
    public void LanguageRejectsAmbiguousConstructionSurfaces()
    {
        LanguageBuilder builder = new LanguageBuilder()
            .ClauseMarker("NEXT");

        LanguageDefinitionException exception = Assert.Throws<LanguageDefinitionException>(() =>
            builder.CommandConnector("NEXT", CommandLinkKind.Sequence))!;

        Assert.That(exception.Message, Does.Contain("clause marker"));
    }

    [Test]
    public void SpecificTwoWordModifierWinsOverGenericIntroducer()
    {
        PromptGrammar grammar = new(
            Array.Empty<KeyValuePair<string, PromptClauseKind>>(),
            Array.Empty<KeyValuePair<string, CommandLinkKind>>(),
            new[]
            {
                new CommandModifierDescriptor("WITH", null, CommandModifierKind.Condition),
                new CommandModifierDescriptor("WITH", "RETRY", CommandModifierKind.Retry)
            });

        ProcessedPrompt prompt = new("SAY value WITH RETRY {2}", grammar);

        Assert.That(prompt.Syntax.Commands.Single().Modifiers.Single().Kind,
            Is.EqualTo(CommandModifierKind.Retry));
    }
}
