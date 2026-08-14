using FluNET.Compilation.Lowering;
using FluNET.Language;
using FluNET.Prompt;
using FluNET.Prompt.Surface;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class SurfaceLoweringTests
{
    [Test]
    public void SayLowersDirectlyToCanonicalPromptSyntaxAndKeepsSourceMap()
    {
        SourceDocument source = new("SAY \"Hello compact\"", SourceSyntaxKind.Compact);
        SurfaceParseResult parsed = new SurfaceParser().Parse(source);
        LoweringResult lowered = new SurfaceLowerer().Lower(
            parsed,
            StandardLanguage.CreateSnapshot().Grammar);

        Assert.Multiple(() =>
        {
            Assert.That(lowered.IsValid, Is.True);
            Assert.That(lowered.CanonicalSyntax.Commands, Has.Count.EqualTo(1));
            Assert.That(lowered.CanonicalSyntax.Commands[0].Verb.Text, Is.EqualTo("SAY"));
            Assert.That(lowered.CanonicalSyntax.Commands[0].Arguments.Single().Text,
                Is.EqualTo("\"Hello compact\""));
            Assert.That(lowered.SourceMap.FindCommand(0),
                Is.EqualTo(new SourceSpan(0, source.Text.Length)));
        });
    }

    [Test]
    public void CompactLoadInfersJsonAndTextOutputsAndParallelizesOneStatement()
    {
        LanguageSnapshot language = StandardLanguage.CreateSnapshot();
        SurfaceParseResult parsed = new SurfaceParser().Parse(new SourceDocument(
            "LOAD post.json, todo.json\nSAY done"));

        LoweringResult lowered = new SurfaceLowerer().Lower(parsed, language.Grammar, language);

        Assert.That(lowered.IsValid, Is.True, string.Join(" ", lowered.Diagnostics.Select(item => item.Message)));
        Assert.That(lowered.CanonicalSyntax.Commands, Has.Count.EqualTo(3));
        Assert.Multiple(() =>
        {
            Assert.That(lowered.CanonicalSyntax.Commands[0].Tokens.Select(token => token.Text),
                Is.EqualTo(new[] { "LOAD", "CONFIG", "[post]", "FROM", "{post.json}" }));
            Assert.That(lowered.CanonicalSyntax.Commands[1].Tokens.Select(token => token.Text),
                Is.EqualTo(new[] { "LOAD", "CONFIG", "[todo]", "FROM", "{todo.json}" }));
            Assert.That(lowered.CanonicalSyntax.Links[0].Kind, Is.EqualTo(CommandLinkKind.Parallel));
            Assert.That(lowered.CanonicalSyntax.Links[1].Kind, Is.EqualTo(CommandLinkKind.Sequence));
            Assert.That(lowered.InferenceTrace.Items, Has.Count.GreaterThanOrEqualTo(8));
        });
    }

    [Test]
    public void CompactLoadUsesExplicitAliasForSingleResource()
    {
        LanguageSnapshot language = StandardLanguage.CreateSnapshot();
        SurfaceParseResult parsed = new SurfaceParser().Parse(new SourceDocument("LOAD post.json AS article"));
        LoweringResult lowered = new SurfaceLowerer().Lower(parsed, language.Grammar, language);

        Assert.Multiple(() =>
        {
            Assert.That(lowered.IsValid, Is.True);
            Assert.That(lowered.CanonicalSyntax.Commands.Single().Tokens.Select(token => token.Text),
                Does.Contain("[article]"));
        });
    }
}
