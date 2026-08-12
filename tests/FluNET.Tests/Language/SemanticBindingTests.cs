using FluNET.Compilation;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Prompt;
using FluNET.Syntax.Verbs;

namespace FluNET.Tests.Language;

[TestFixture]
public sealed class SemanticBindingTests
{
    private SemanticCommandBinder _binder = null!;

    [SetUp]
    public void SetUp() =>
        _binder = new SemanticCommandBinder(StandardLanguage.CreateSnapshot());

    [Test]
    public void Bind_AssignsCanonicalCommandFrameAndRoles()
    {
        CommandSyntax syntax = Parse("FETCH [result] FROM {input.txt}.");

        BoundCommand command = _binder.Bind(syntax);

        Assert.Multiple(() =>
        {
            Assert.That(command.Command.Name, Is.EqualTo("GET"));
            Assert.That(command.Frame.ImplementationType, Is.EqualTo(typeof(GetText)));
            Assert.That(command[SemanticRole.Output].Tokens.Single().Text, Is.EqualTo("[result]"));
            Assert.That(command[SemanticRole.Output].Slot.Direction, Is.EqualTo(SlotDirection.Output));
            Assert.That(command[SemanticRole.Source].Tokens.Single().Text, Is.EqualTo("{input.txt}"));
        });
    }

    [TestCase("LOAD config FROM {settings.json}.", typeof(LoadConfig))]
    [TestCase("LOAD [configname] FROM {settings.json}.", typeof(LoadConfig))]
    [TestCase("LOAD [text] FROM {input.txt}.", typeof(LoadText))]
    public void Bind_SelectsOneRealization(string source, Type implementationType)
    {
        BoundCommand command = _binder.Bind(Parse(source));

        Assert.That(command.Frame.ImplementationType, Is.EqualTo(implementationType));
    }

    [Test]
    public void Bind_ConsumesAnExplicitQualifierBeforeTheSubject()
    {
        BoundCommand command = _binder.Bind(
            Parse("LOAD CONFIG [settings] FROM {settings.json}."));

        Assert.Multiple(() =>
        {
            Assert.That(command.Frame.ImplementationType, Is.EqualTo(typeof(LoadConfig)));
            Assert.That(command[SemanticRole.Output].Tokens.Select(token => token.Text),
                Is.EqualTo(new[] { "[settings]" }));
        });
    }

    [Test]
    public void Bind_RepresentsAnAbsentOptionalRole()
    {
        BoundCommand command = _binder.Bind(Parse("DELETE {file.txt}."));

        Assert.Multiple(() =>
        {
            Assert.That(command[SemanticRole.Theme].IsPresent, Is.True);
            Assert.That(command[SemanticRole.Source].IsPresent, Is.False);
            Assert.That(command[SemanticRole.Source].Slot.Cardinality,
                Is.EqualTo(SlotCardinality.Optional));
        });
    }

    [Test]
    public void SemanticValidation_RejectsAMissingRequiredSemanticRole()
    {
        ProcessedPrompt prompt = new("SAVE content.", _binder.Language.Grammar);
        BoundCommand command = _binder.Bind(prompt.Syntax.Commands.Single());
        BoundProgram program = new(
            new FluNetProgram(prompt),
            [new BoundCommandStatement(command)]);

        DiagnosticBag diagnostics = new SemanticProgramValidator(_binder.Language).Validate(program);
        CompilationDiagnostic diagnostic = diagnostics.Single(item =>
            item.Code == CompilationDiagnosticCodes.MissingRequiredRole);

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Phase, Is.EqualTo(CompilationPhase.Validate));
            Assert.That(diagnostic.Message, Does.Contain("TO clause"));
        });
    }

    [Test]
    public void Language_RequiresOneDefaultForAMultiFrameCommand()
    {
        LanguageBuilder builder = new();
        builder.Command<LoadText, string[]>("IMPORT", "Text")
            .Qualifiers("TEXT")
            .Positional<string[]>(SemanticRole.Output, SlotDirection.Output);
        builder.Command<LoadConfig, Dictionary<string, object>>("IMPORT", "Config")
            .Qualifiers("CONFIG")
            .Positional<Dictionary<string, object>>(SemanticRole.Output, SlotDirection.Output);

        LanguageDefinitionException? error = Assert.Throws<LanguageDefinitionException>(() => builder.Build());

        Assert.That(error!.Message, Does.Contain("exactly one default frame"));
    }

    private static CommandSyntax Parse(string source) =>
        new ProcessedPrompt(source).Syntax.Commands.Single();
}
