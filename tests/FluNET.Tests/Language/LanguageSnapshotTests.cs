using FluNET.Language;
using FluNET.Syntax.Registry;
using FluNET.Syntax.Verbs;

namespace FluNET.Tests.Language;

[TestFixture]
public sealed class LanguageSnapshotTests
{
    [Test]
    public void StandardLanguage_DescribesAliasesFramesAndSemanticRoles()
    {
        LanguageSnapshot snapshot = StandardLanguage.CreateSnapshot();

        CommandDescriptor? get = snapshot.FindCommand("fetch");
        CommandDescriptor? load = snapshot.FindCommand("LOAD");

        Assert.Multiple(() =>
        {
            Assert.That(get, Is.Not.Null);
            Assert.That(get!.Name, Is.EqualTo("GET"));
            Assert.That(get.Aliases, Does.Contain("RETRIEVE"));
            Assert.That(get.Frames.Single().Slots.Select(slot => slot.Role),
                Is.EqualTo(new[] { SemanticRole.Output, SemanticRole.Source }));
            Assert.That(get.Frames.Single().Slots[1].Marker, Is.EqualTo("FROM"));
            Assert.That(load, Is.Not.Null);
            Assert.That(load!.Frames.Select(frame => frame.UsageName),
                Is.EquivalentTo(new[] { "Text", "Config" }));
        });
    }

    [Test]
    public void Build_FreezesAnIndependentSnapshot()
    {
        LanguageBuilder builder = new();
        builder.Command<SayText, string>("SPEAK", "Text")
            .Aliases("TELL")
            .Positional<string>(SemanticRole.Theme);

        LanguageSnapshot first = builder.Build();

        builder.Command<GetText, string[]>("READ", "Text")
            .Positional<string[]>(SemanticRole.Output, SlotDirection.Output);
        LanguageSnapshot second = builder.Build();

        Assert.Multiple(() =>
        {
            Assert.That(first.FindCommand("SPEAK"), Is.Not.Null);
            Assert.That(first.FindCommand("READ"), Is.Null);
            Assert.That(second.FindCommand("READ"), Is.Not.Null);
        });
    }

    [Test]
    public void Build_RejectsAmbiguousSurfaceForms()
    {
        LanguageBuilder builder = new();
        builder.Command<SayText, string>("SPEAK", "Text")
            .Aliases("SHARED")
            .Positional<string>(SemanticRole.Theme);
        builder.Command<GetText, string[]>("READ", "Text")
            .Aliases("SHARED")
            .Positional<string[]>(SemanticRole.Output, SlotDirection.Output);

        LanguageDefinitionException? error = Assert.Throws<LanguageDefinitionException>(() => builder.Build());

        Assert.That(error!.Message, Does.Contain("SHARED"));
    }

    [Test]
    public void Registry_ProjectsOnlyTheInjectedSnapshot()
    {
        LanguageBuilder builder = new();
        builder.Command<SayText, string>("SPEAK", "Text")
            .Positional<string>(SemanticRole.Theme);
        LanguageSnapshot snapshot = builder.Build();

        LanguageRegistry registry = new(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(registry.Snapshot, Is.SameAs(snapshot));
            Assert.That(registry.GetVerbType("SPEAK"), Is.EqualTo(typeof(SayText)));
            Assert.That(registry.GetVerbType("GET"), Is.Null);
            Assert.That(registry.Verbs, Is.EqualTo(new[] { typeof(SayText) }));
        });
    }
}
