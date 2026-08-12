using FluNET.Language;
using FluNET.Syntax.Verbs;

namespace FluNET.Tests.Language;

[TestFixture]
public sealed class TypeSystemTests
{
    [Test]
    public void ModuleCanDeclareFrameSpecificRolesAndTypeNames()
    {
        FrameRoleId condition = new("Condition");
        LanguageBuilder builder = new LanguageBuilder().Type<Predicate>("Predicate");
        builder.Command<SayText, string>("CHECK", "Predicate")
            .Positional<Predicate>(condition);

        LanguageSnapshot language = builder.Build();
        CommandSlotDescriptor slot = language.FindCommand("CHECK")!.Frames.Single().Slots.Single();

        Assert.Multiple(() =>
        {
            Assert.That(slot.RoleId, Is.EqualTo(condition));
            Assert.That(slot.ValueTypeSymbol.Name, Is.EqualTo("Predicate"));
            Assert.That(language.Types.Get<string>().Name, Is.EqualTo("Text"));
        });
    }

    private sealed record Predicate(string Text);
}
