using FluNET.Context;
using FluNET.Prompt;
<<<<<<< HEAD
using FluNET.Tokens.Tree;
=======
>>>>>>> origin/agent/stabilize-poc-foundation

namespace FluNET.Tests;

[TestFixture]
public sealed class SyntaxTests
{
    private FluNETContext _context = null!;
    private Engine _engine = null!;

    [SetUp]
    public void SetUp()
    {
        _context = FluNETContext.Create();
        _engine = _context.GetEngine();
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    [Test]
    public void Tokenizer_PreservesStructuredValuesAndSeparatesAttachedTerminator()
    {
        ProcessedPrompt prompt = new("GET [text] FROM {C:\\Test Files\\document.txt}.");

        Assert.That(prompt.Tokens, Is.EqualTo(new[]
        {
            "GET", "[text]", "FROM", "{C:\\Test Files\\document.txt}", "."
        }));
        Assert.That(prompt.IsValid, Is.True);
    }

    [Test]
    public void Tokenizer_PreservesQuotedTextAndEscapes()
    {
        ProcessedPrompt prompt = new("SAY \"hello \\\"FluNET\\\" world\".");

        Assert.That(prompt.Tokens, Is.EqualTo(new[]
        {
            "SAY", "\"hello \\\"FluNET\\\" world\"", "."
        }));
        Assert.That(prompt.IsValid, Is.True);
    }

    [TestCase("GET [text FROM {file.txt}.", "FLN003")]
    [TestCase("GET [text] FROM {file.txt].", "FLN001")]
    [TestCase("SAY \"unterminated.", "FLN002")]
    [TestCase("SAY ok THEN.", "FLN004")]
    public void Tokenizer_ReportsMalformedInput(string text, string expectedCode)
    {
        ProcessedPrompt prompt = new(text);

        Assert.Multiple(() =>
        {
            Assert.That(prompt.IsValid, Is.False);
            Assert.That(prompt.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(expectedCode));
        });
    }

    [Test]
    public void SyntaxTree_RepresentsThenCommands()
    {
        ProcessedPrompt prompt = new("SAY one THEN SAY two.");

        Assert.Multiple(() =>
        {
            Assert.That(prompt.Syntax.Commands, Has.Count.EqualTo(2));
            Assert.That(prompt.Syntax.Commands[0].Tokens.Select(token => token.Text),
                Is.EqualTo(new[] { "SAY", "one" }));
            Assert.That(prompt.Syntax.Commands[1].Tokens.Select(token => token.Text),
                Is.EqualTo(new[] { "SAY", "two" }));
        });
    }

    [Test]
<<<<<<< HEAD
    public void SyntaxTree_ExposesStableSourceSpans()
    {
        ProcessedPrompt prompt = new("SAY one THEN SAY two.");

        Assert.Multiple(() =>
        {
            Assert.That(prompt.SourceText, Is.EqualTo("SAY one THEN SAY two."));
            Assert.That(prompt.Syntax.Span, Is.EqualTo(new SourceSpan(0, 20)));
            Assert.That(prompt.Syntax.Commands[0].Span, Is.EqualTo(new SourceSpan(0, 7)));
            Assert.That(prompt.Syntax.Commands[1].Span, Is.EqualTo(new SourceSpan(13, 7)));
            Assert.That(prompt.Syntax.Commands[1].Verb.Span, Is.EqualTo(new SourceSpan(13, 3)));
        });
    }

    [Test]
    public void SyntaxTree_TakesDefensiveCollectionSnapshots()
    {
        List<PromptToken> tokens = [new("SAY", PromptTokenKind.Word, 0, 3)];
        CommandSyntax command = new(tokens);
        List<CommandSyntax> commands = [command];
        PromptSyntax syntax = new(commands);

        tokens.Clear();
        commands.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(command.Tokens, Has.Count.EqualTo(1));
            Assert.That(syntax.Commands, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void TokenTreeAdapter_UsesParserCommandBoundaries()
    {
        ProcessedPrompt prompt = new("SAY one THEN SAY two.");
        TokenTreeFactory factory = _context.GetService<TokenTreeFactory>();

        IReadOnlyList<TokenTree> commands = factory.ProcessCommands(prompt);

        Assert.Multiple(() =>
        {
            Assert.That(commands, Has.Count.EqualTo(2));
            Assert.That(commands[0].GetTokens().Select(token => token.Value),
                Is.EqualTo(new[] { "SAY", "one" }));
            Assert.That(commands[1].GetTokens().Select(token => token.Value),
                Is.EqualTo(new[] { "SAY", "two" }));
        });
    }

    [Test]
=======
>>>>>>> origin/agent/stabilize-poc-foundation
    public void SyntaxTree_RepresentsVerbAndClauses()
    {
        CommandSyntax command = new ProcessedPrompt(
            "DOWNLOAD [file] FROM {https://example.test/a.txt} TO {a.txt}.")
            .Syntax.Commands.Single();

        Assert.Multiple(() =>
        {
            Assert.That(command.Verb.Text, Is.EqualTo("DOWNLOAD"));
            Assert.That(command.Clauses.Select(clause => clause.Kind), Is.EqualTo(new[]
            {
                PromptClauseKind.Subject,
                PromptClauseKind.From,
                PromptClauseKind.To
            }));
            Assert.That(command.Clauses[1].Values.Single().Text,
                Is.EqualTo("{https://example.test/a.txt}"));
        });
    }

    [TestCase("GET [text] FROM {file.txt}.")]
    [TestCase("GET [text] FROM {file.txt}")]
    [TestCase("GET TEXT [text] FROM {file.txt}?")]
    [TestCase("SAVE \"hello world\" TO {output.txt}!")]
    [TestCase("LOAD config FROM {settings.json}.")]
    public void Analyze_AcceptsValidCommandsWithoutExecutingThem(string text)
    {
        PromptAnalysis analysis = _engine.Analyze(new ProcessedPrompt(text));

        Assert.Multiple(() =>
        {
            Assert.That(analysis.IsValid, Is.True, analysis.ValidationResult.FailureReason);
            Assert.That(analysis.Sentence, Is.Not.Null);
        });
    }

    [TestCase("", "Empty")]
    [TestCase(".", "Empty")]
    [TestCase("UPLOAD [data] TO {out.txt}.", "known verb")]
    [TestCase("GET FROM {file.txt}.", "subject")]
    [TestCase("GET [text] FROM.", "value")]
    [TestCase("GET TEXT FROM {file.txt}.", "followed by")]
    [TestCase("SAVE value.", "TO")]
    [TestCase("SAY ok THEN", "THEN")]
    public void Analyze_RejectsInvalidCommands(string text, string reasonFragment)
    {
        PromptAnalysis analysis = _engine.Analyze(new ProcessedPrompt(text));

        Assert.Multiple(() =>
        {
            Assert.That(analysis.IsValid, Is.False);
            Assert.That(analysis.ValidationResult.FailureReason,
                Does.Contain(reasonFragment).IgnoreCase);
        });
    }
}
