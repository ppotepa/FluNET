using FluNET.Prompt.Surface;

namespace FluNET.Tests.Prompt;

[TestFixture]
public sealed class SurfaceParserTests
{
    [Test]
    public void ParserKeepsMultipleValuesAndAliasWithoutInference()
    {
        SurfaceParseResult result = new SurfaceParser().Parse(new SourceDocument(
            "LOAD post.json, todo.json AS documents\nSAY \"{post.title} — {todo.title}\""));

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Program.Statements, Has.Count.EqualTo(2));
        SurfaceCommandSyntax load = (SurfaceCommandSyntax)result.Program.Statements[0];
        SurfaceCommandSyntax say = (SurfaceCommandSyntax)result.Program.Statements[1];
        Assert.Multiple(() =>
        {
            Assert.That(load.NormalizedName, Is.EqualTo("LOAD"));
            Assert.That(load.Values.Select(value => value.Text),
                Is.EqualTo(new[] { "post.json", "todo.json" }));
            Assert.That(load.Alias, Is.EqualTo("documents"));
            Assert.That(say.Values, Has.Count.EqualTo(1));
            Assert.That(say.Values[0].Text, Is.EqualTo("\"{post.title} — {todo.title}\""));
        });
    }

    [Test]
    public void ParserDoesNotSplitCommasInsideQuotedOrStructuredValues()
    {
        SurfaceParseResult result = new SurfaceParser().Parse(new SourceDocument(
            "GET sql:\"SELECT a,b FROM t\", OBJECT(a: 1, b: 2)"));

        SurfaceCommandSyntax command = (SurfaceCommandSyntax)result.Program.Statements.Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True);
            Assert.That(command.Values, Has.Count.EqualTo(2));
        });
    }
}
