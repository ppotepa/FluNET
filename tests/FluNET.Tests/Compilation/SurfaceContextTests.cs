using FluNET.Compilation.Lowering;
using FluNET.Language;
using FluNET.Prompt.Surface;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class SurfaceContextTests
{
    [Test]
    public void ParserBuildsLexicalFromBlock()
    {
        const string source = """
FROM https://api.example.test/
    GET posts/1 AS post
    SAY "{post.title}"
""";
        SurfaceParseResult parsed = new SurfaceParser().Parse(new SourceDocument(source));

        Assert.That(parsed.IsValid, Is.True, string.Join(" | ", parsed.Diagnostics.Select(item => item.Message)));
        Assert.That(parsed.Program.Statements, Has.Count.EqualTo(1));
        Assert.That(parsed.Program.Statements[0], Is.TypeOf<SurfaceContextSyntax>());
        SurfaceContextSyntax context = (SurfaceContextSyntax)parsed.Program.Statements[0];
        Assert.That(context.Statements, Has.Count.EqualTo(2));
    }

    [Test]
    public void FromContextResolvesRelativeUrlsAndLowersPolicies()
    {
        const string source = """
FROM https://api.example.test/
    RETRY 3
    TIMEOUT 10s
    GET posts/1 AS post
    GET todos/1 AS todo
    SAY "{post.title} — {todo.title}"
""";
        FluNetRuntimeDefinition runtime = SurfaceLanguage.CreateRuntime();
        SurfaceParseResult parsed = new SurfaceParser().Parse(new SourceDocument(source));
        LoweringResult lowered = new SurfaceLowerer().Lower(parsed, runtime.Language.Grammar, runtime.Language);

        Assert.That(lowered.IsValid, Is.True, string.Join(" | ", lowered.Diagnostics.Select(item => item.Message)));
        Assert.That(lowered.CanonicalSyntax.Commands, Has.Count.EqualTo(3));
        Assert.Multiple(() =>
        {
            Assert.That(lowered.CanonicalSyntax.Commands[0].Tokens.Select(token => token.Text),
                Does.Contain("{https://api.example.test/posts/1}"));
            Assert.That(lowered.CanonicalSyntax.Commands[1].Tokens.Select(token => token.Text),
                Does.Contain("{https://api.example.test/todos/1}"));
            Assert.That(lowered.CanonicalSyntax.Commands[0].Modifiers.Select(modifier => modifier.Kind),
                Is.EquivalentTo(new[] { FluNET.Prompt.CommandModifierKind.Retry, FluNET.Prompt.CommandModifierKind.Timeout }));
            Assert.That(lowered.InferenceTrace.Items.Any(item => item.Rule == "lexical-base-uri"), Is.True);
        });
    }

    [Test]
    public void UseCreatesNamedBaseWithoutRuntimeCommand()
    {
        const string source = """
USE https://api.example.test AS api
GET api/posts/1 AS post
""";
        FluNetRuntimeDefinition runtime = SurfaceLanguage.CreateRuntime();
        LoweringResult lowered = new SurfaceLowerer().Lower(
            new SurfaceParser().Parse(new SourceDocument(source)),
            runtime.Language.Grammar,
            runtime.Language);

        Assert.Multiple(() =>
        {
            Assert.That(lowered.IsValid, Is.True);
            Assert.That(lowered.CanonicalSyntax.Commands, Has.Count.EqualTo(1));
            Assert.That(lowered.CanonicalSyntax.Commands[0].Tokens.Select(token => token.Text),
                Does.Contain("{https://api.example.test/posts/1}"));
        });
    }
}
