using FluNET.Compilation.Lowering;
using FluNET.Language;
using FluNET.Prompt.Surface;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class SurfaceInterpolationLoweringTests
{
    [Test]
    public void UnquotedPropertyPathLowersToNormalSayInterpolationToken()
    {
        LanguageSnapshot language = StandardLanguage.CreateSnapshot();
        SurfaceParseResult parsed = new SurfaceParser().Parse(new SourceDocument("SAY post.title"));
        LoweringResult lowered = new SurfaceLowerer().Lower(parsed, language.Grammar, language);

        Assert.Multiple(() =>
        {
            Assert.That(lowered.IsValid, Is.True);
            Assert.That(lowered.CanonicalSyntax.Commands.Single().Arguments.Single().Text,
                Is.EqualTo("\"{post.title}\""));
        });
    }
}
