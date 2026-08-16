using FluNET.Language;
using FluNET.Language.Metadata;
using FluNET.Syntax.Core;

namespace FluNET.Tests;

public class LanguageCompilerIdentityTests
{
    [Test]
    public void Verb_attribute_defines_identity_without_instantiating_the_type()
    {
        var compiler = new LanguageCompiler();
        VerbIdentity? identity = compiler.DescribeVerbIdentity(typeof(AbstractAttributedVerb));
        Assert.That(identity, Is.Not.Null);
        Assert.That(identity!.Text, Is.EqualTo("CUSTOM"));
        Assert.That(identity.Synonyms, Does.Contain("ALT"));
    }

    [Test]
    public void Semantic_family_marker_defines_standard_keyword()
    {
        var compiler = new LanguageCompiler();
        VerbIdentity? identity = compiler.DescribeVerbIdentity(typeof(AbstractGetVerb));
        Assert.That(identity, Is.Not.Null);
        Assert.That(identity!.Text, Is.EqualTo("GET"));
    }

    [Verb("CUSTOM")]
    [Alias("ALT")]
    private abstract class AbstractAttributedVerb : IVerb { }

    private abstract class AbstractGetVerb : IGet { }
}
