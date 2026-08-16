using FluNET.Language;

namespace FluNET.Tests;

public class LanguageRegistryTests
{
    [Fact]
    public void Registry_discovers_standard_verbs_and_builds_sentence_patterns()
    {
        var registry = new LanguageRegistry();

        Assert.True(registry.TryGetVerb("GET", out VerbDescriptor? get));
        Assert.NotNull(get);
        Assert.Contains(get!.Pattern.Clauses, x => x.Kind == ClauseKind.What);
        Assert.Contains(get.Pattern.Clauses, x => x.Kind == ClauseKind.From);
    }

    [Theory]
    [InlineData("TEXT")]
    [InlineData("JSON")]
    [InlineData("BINARY")]
    public void Standard_qualifiers_are_registry_entries(string qualifier)
    {
        var registry = new LanguageRegistry();
        Assert.True(registry.IsQualifier(qualifier));
    }

    [Fact]
    public void Modules_can_extend_qualifiers_without_changing_word_factory()
    {
        var registry = new LanguageRegistry();
        registry.RegisterQualifier("PARQUET");

        Assert.True(registry.IsQualifier("PARQUET"));
    }
}
