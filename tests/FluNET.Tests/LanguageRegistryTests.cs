using FluNET.Language;

namespace FluNET.Tests;

public class LanguageRegistryTests
{
    [Test]
    public void Registry_discovers_standard_verbs_and_builds_sentence_patterns()
    {
        var registry = new LanguageRegistry();
        Assert.That(registry.TryGetVerb("GET", out VerbDescriptor? get), Is.True);
        Assert.That(get, Is.Not.Null);
        Assert.That(get!.Pattern.Clauses.Any(x => x.Kind == ClauseKind.What), Is.True);
        Assert.That(get.Pattern.Clauses.Any(x => x.Kind == ClauseKind.From), Is.True);
    }

    [TestCase("TEXT")]
    [TestCase("JSON")]
    [TestCase("BINARY")]
    public void Standard_qualifiers_are_registry_entries(string qualifier)
    {
        var registry = new LanguageRegistry();
        Assert.That(registry.IsQualifier(qualifier), Is.True);
    }

    [Test]
    public void Modules_can_extend_qualifiers_without_changing_word_factory()
    {
        var registry = new LanguageRegistry();
        registry.RegisterQualifier("PARQUET");
        Assert.That(registry.IsQualifier("PARQUET"), Is.True);
    }
}
