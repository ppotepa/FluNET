using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Prompt;
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

    [Test]
    public void CustomRoleDiagnosticsDoNotDependOnTheCompatibilityEnum()
    {
        FrameRoleId comparison = new("Comparison");
        LanguageBuilder builder = new();
        builder.Command<SayText, string>("CHECK", "Text")
            .Positional<string>(SemanticRole.Theme)
            .Marked<string>(comparison, "AS");
        LanguageSnapshot language = builder.Build();
        ProcessedPrompt prompt = new("CHECK value AS first second", language.Grammar);

        SemanticBindingException exception = Assert.Throws<SemanticBindingException>(() =>
            new SemanticCommandBinder(language).BindProgram(prompt.Syntax))!;

        Assert.That(exception.Message, Does.Contain("COMPARISON"));
    }

    private sealed record Predicate(string Text);
}
