using FluNET.Compilation.Lowering;
using FluNET.Language;
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
            Assert.That(lowered.SourceMap.FindCommand(0), Is.EqualTo(source.Text.Length == 0
                ? (FluNET.Prompt.SourceSpan?)null
                : new FluNET.Prompt.SourceSpan(0, source.Text.Length)));
        });
    }
}
