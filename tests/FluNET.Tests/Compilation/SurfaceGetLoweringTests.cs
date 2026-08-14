using FluNET.Compilation.Lowering;
using FluNET.Language;
using FluNET.Prompt.Surface;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class SurfaceGetLoweringTests
{
    [Test]
    public void HttpGetLowersToGeneratedJsonResourceCommand()
    {
        FluNetRuntimeDefinition runtime = SurfaceLanguage.CreateRuntime();
        SurfaceParseResult parsed = new SurfaceParser().Parse(new SourceDocument(
            "GET https://api.example.test/posts/1 AS post"));

        LoweringResult lowered = new SurfaceLowerer().Lower(parsed, runtime.Language.Grammar, runtime.Language);

        Assert.Multiple(() =>
        {
            Assert.That(lowered.IsValid, Is.True, string.Join(" | ", lowered.Diagnostics.Select(item => item.Message)));
            Assert.That(lowered.CanonicalSyntax.Commands.Single().Tokens.Select(token => token.Text),
                Is.EqualTo(new[] { "GETHTTP", "[post]", "FROM", "{https://api.example.test/posts/1}" }));
            Assert.That(runtime.Language.FindFrame(new FrameId("surface.get.http.json")), Is.Not.Null);
        });
    }

    [Test]
    public void EnvironmentGetLowersToGeneratedEnvironmentRead()
    {
        FluNetRuntimeDefinition runtime = SurfaceLanguage.CreateRuntime();
        SurfaceParseResult parsed = new SurfaceParser().Parse(new SourceDocument("GET env:DATABASE_URL"));

        LoweringResult lowered = new SurfaceLowerer().Lower(parsed, runtime.Language.Grammar, runtime.Language);

        Assert.Multiple(() =>
        {
            Assert.That(lowered.IsValid, Is.True);
            Assert.That(lowered.CanonicalSyntax.Commands.Single().Tokens.Select(token => token.Text),
                Is.EqualTo(new[] { "GETENV", "[database_url]", "FROM", "{DATABASE_URL}" }));
        });
    }

    [Test]
    public void LocalGetReusesLoadInferenceAndSecretRequiresProvider()
    {
        FluNetRuntimeDefinition runtime = SurfaceLanguage.CreateRuntime();
        LoweringResult file = new SurfaceLowerer().Lower(
            new SurfaceParser().Parse(new SourceDocument("GET settings.json")),
            runtime.Language.Grammar,
            runtime.Language);
        LoweringResult secret = new SurfaceLowerer().Lower(
            new SurfaceParser().Parse(new SourceDocument("GET secret:github-token")),
            runtime.Language.Grammar,
            runtime.Language);

        Assert.Multiple(() =>
        {
            Assert.That(file.IsValid, Is.True);
            Assert.That(file.CanonicalSyntax.Commands.Single().Verb.Text, Is.EqualTo("LOAD"));
            Assert.That(secret.IsValid, Is.False);
            Assert.That(secret.Diagnostics.Select(item => item.Code), Does.Contain("FLN234"));
        });
    }
}
