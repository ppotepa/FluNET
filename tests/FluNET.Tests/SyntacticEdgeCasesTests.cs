using FluNET.Context;
using FluNET.Prompt;

namespace FluNET.Tests;

[TestFixture]
public sealed class SyntacticEdgeCasesTests
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

    [TestCase("get [value] from {./relative file.txt}.")]
    [TestCase("  GET\t[value]\tFROM\t{C:\\dane ąćęłńóśźż.txt}  .  ")]
    [TestCase("GET [value123] FROM {network-share/path_01.txt}?")]
    [TestCase("SAVE 'line one\\nline two' TO {file with spaces.txt}!")]
    [TestCase("SAY \"\".")]
    public void Analyze_HandlesWhitespaceCaseUnicodeAndQuotedValues(string text)
    {
        PromptAnalysis analysis = _engine.Analyze(new ProcessedPrompt(text));

        Assert.That(analysis.IsValid, Is.True, analysis.ValidationResult.FailureReason);
    }

    [TestCase("GET [] FROM {file.txt}.")]
    [TestCase("GET [value] FROM {}.")]
    [TestCase("GET [value FROM {file.txt}.")]
    [TestCase("GET [value] FROM {file.txt.")]
    [TestCase("THEN SAY hello.")]
    [TestCase("SAY hello THEN THEN SAY world.")]
    public void Analyze_RejectsEmptyOrMalformedStructuredValues(string text)
    {
        PromptAnalysis analysis = _engine.Analyze(new ProcessedPrompt(text));

        Assert.That(analysis.IsValid, Is.False);
    }

    [Test]
    public void ToString_NormalizesSpacingWithoutCorruptingReference()
    {
        ProcessedPrompt prompt = new("GET   [value] FROM {file.txt}.");

        Assert.That(prompt.ToString(), Is.EqualTo("GET [value] FROM {file.txt}."));
        Assert.That(prompt.Tokens, Does.Contain("{file.txt}"));
    }

    [Test]
    public void VeryLongStructuredValues_RemainSingleTokens()
    {
        string name = new('x', 10_000);
        ProcessedPrompt prompt = new($"GET [{name}] FROM {{{name}.txt}}.");

        Assert.Multiple(() =>
        {
            Assert.That(prompt.IsValid, Is.True);
            Assert.That(prompt.Tokens, Has.Length.EqualTo(5));
            Assert.That(prompt.Tokens[1], Has.Length.EqualTo(10_002));
            Assert.That(prompt.Tokens[3], Has.Length.EqualTo(10_006));
        });
    }
}
