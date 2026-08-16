using FluNET.Language;
using FluNET.Language.Metadata;
using FluNET.Syntax.Core;

namespace FluNET.Tests;

public class LanguageCompilerIdentityTests
{
    [Fact]
    public void Verb_attribute_defines_identity_without_instantiating_the_type()
    {
        var compiler = new LanguageCompiler();

        VerbIdentity? identity = compiler.DescribeVerbIdentity(typeof(AbstractAttributedVerb));

        Assert.NotNull(identity);
        Assert.Equal("CUSTOM", identity!.Text);
        Assert.Contains("ALT", identity.Synonyms);
    }

    [Fact]
    public void Semantic_family_marker_defines_standard_keyword()
    {
        var compiler = new LanguageCompiler();

        VerbIdentity? identity = compiler.DescribeVerbIdentity(typeof(AbstractGetVerb));

        Assert.NotNull(identity);
        Assert.Equal("GET", identity!.Text);
    }

    [Verb("CUSTOM")]
    [Alias("ALT")]
    private abstract class AbstractAttributedVerb : IVerb
    {
    }

    private abstract class AbstractGetVerb : IGet
    {
    }
}
