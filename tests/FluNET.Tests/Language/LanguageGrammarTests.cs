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
}
