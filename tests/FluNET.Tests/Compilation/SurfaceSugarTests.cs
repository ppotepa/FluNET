using FluNET.Context;
using FluNET.Capabilities;
using FluNET.Compilation;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class SurfaceSugarTests
{
    [Test]
    public void WhereAndOrderByAreCompactAliasesForDataPipelines()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var result = context.CompileSurface(
            "GET https://example.test/posts AS posts\nWHERE published == true\nORDER BY title");

        Assert.That(result.IsValid, Is.True,
            string.Join("; ", result.SurfaceParse.Diagnostics.Select(d => d.Message)
                .Concat(result.Lowering.Diagnostics.Select(d => d.Message))) +
            " | " + string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.That(result.Plan!.Steps.Select(step => step.Command.Frame.Id.Value),
            Is.EqualTo(new[]
            {
                "surface.get.http.json",
                "surface.data.filter.json",
                "surface.data.sort.json"
            }));
    }

    [Test]
    public void SaveToUsesTheCurrentPipelineValue()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var result = context.CompileSurface(
            "GET post.txt AS post\nSAVE TO report.txt");

        Assert.That(result.IsValid, Is.True,
            string.Join("; ", result.SurfaceParse.Diagnostics.Select(d => d.Message)
                .Concat(result.Lowering.Diagnostics.Select(d => d.Message))) +
            " | " + string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.That(result.Plan!.Steps.Last().Command.Frame.Id.Value,
            Is.EqualTo("core.save.text"));
    }

    [Test]
    public void SaveJsonInfersEncodingFromTheTargetExtension()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var result = context.CompileSurface(
            "GET https://example.test/posts AS posts\nSAVE TO report.json");

        Assert.That(result.IsValid, Is.True,
            string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.That(result.Plan!.Steps.Last().Command.Frame.Id.Value,
            Is.EqualTo("core.save.json"));
    }

    [Test]
    public void ExplicitSaveJsonUsesJsonEncodingAndNormalizesAQuotedTarget()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var result = context.CompileSurface(
            "GET https://example.test/posts AS posts\nSAVE JSON [posts] TO \"./report.json\"");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True,
                string.Join("; ", result.Diagnostics.Select(d => d.Message)));
            Assert.That(result.Plan!.Steps.Last().Command.Frame.Id.Value, Is.EqualTo("core.save.json"));
            Assert.That(result.Lowering.CanonicalSyntax.Commands.Last().AllTokens.Last().Text,
                Is.EqualTo("{./report.json}"));
        });
    }

    [Test]
    public void PostToUsesTheCurrentPipelineValue()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var result = context.CompileSurface(
            "GET post.txt AS post\nPOST TO https://example.test/archive");

        Assert.That(result.IsValid, Is.True,
            string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.That(result.Plan!.Steps.Last().Command.Frame.Id.Value,
            Is.EqualTo("core.post.json"));
    }

    [Test]
    public void DistinctAndSkipComposeWithTheImplicitPipeline()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var result = context.CompileSurface(
            "GET https://example.test/posts AS posts | DISTINCT BY userId | SKIP 2 | TAKE 3");

        Assert.That(result.IsValid, Is.True,
            string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.That(result.Plan!.Steps.Select(step => step.Command.Frame.Id.Value),
            Is.EqualTo(new[]
            {
                "surface.get.http.json",
                "surface.data.distinct.json",
                "surface.data.skip.json",
                "surface.data.take.json"
            }));
    }

    [Test]
    public void AggregatesAreAvailableAsScalarPipelineOperations()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var result = context.CompileSurface(
            "GET https://example.test/posts AS posts | COUNT");

        Assert.That(result.IsValid, Is.True,
            string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.That(result.Plan!.Steps.Last().Command.Frame.Id.Value,
            Is.EqualTo("surface.data.count.json"));
    }

    [Test]
    public void LetLowersToTypedSetWithoutASeparateDeclarationForm()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var result = context.CompileSurface(
            "LET limit = 10\nLET enabled = true");

        Assert.That(result.IsValid, Is.True,
            string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.That(result.Plan!.Steps.Select(step => step.Command.Frame.Id.Value),
            Is.EqualTo(new[] { "core.set.number", "core.set.boolean" }));
    }

    [Test]
    public void NullSafePropertyAccessComposesWithCoalescing()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var result = context.CompileSurface(
            "GET https://example.test/posts AS posts | WHERE user?.address?.city ?? \"Unknown\"");

        Assert.That(result.IsValid, Is.True,
            string.Join("; ", result.SurfaceParse.Diagnostics.Select(d => d.Message)
                .Concat(result.Lowering.Diagnostics.Select(d => d.Message))) +
            " | " + string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.That(result.Plan!.Steps.Last().Command.Frame.Id.Value,
            Is.EqualTo("surface.data.filter.json"));
    }

    [Test]
    public void LetSupportsStructuredObjectLiterals()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var result = context.CompileSurface("LET config = { retries: 3, enabled: true }");

        Assert.That(result.IsValid, Is.True,
            string.Join("; ", result.SurfaceParse.Diagnostics.Select(d => d.Message)
                .Concat(result.Lowering.Diagnostics.Select(d => d.Message))) +
            " | " + string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.That(result.Plan!.Steps.Single().Command.Frame.Id.Value,
            Is.EqualTo("core.set.json"));
    }

    [Test]
    public void ReadFilesLoadsSeveralResourcesWithoutRepeatingTheSentence()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult result = context.CompileSurface(
            "READ FILES \"post.json\", \"todo.json\"");

        Assert.That(result.IsValid, Is.True,
            string.Join("; ", result.Diagnostics.Select(item => item.Message)
                .Concat(result.Lowering.Diagnostics.Select(item => item.Message))));
        Assert.That(result.Plan!.Steps, Has.Count.EqualTo(2));
        Assert.That(result.Plan.Steps.Select(step => step.Command.Frame.Id.Value).ToArray(),
            Has.All.Not.Null);
        Assert.That(result.Plan.Steps.Select(step => step.Command.Frame.Id.Value).ToArray().Length,
            Is.EqualTo(2));
    }

    [TestCase("LOAD FILES \"post.json\", \"todo.json\"")]
    [TestCase("GET FILES \"post.json\", \"todo.json\"")]
    public void LoadAndGetFilesShareTheBatchResourceSugar(string source)
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult result = context.CompileSurface(source);

        Assert.That(result.IsValid, Is.True,
            string.Join("; ", result.Diagnostics.Select(item => item.Message)
                .Concat(result.Lowering.Diagnostics.Select(item => item.Message))));
        Assert.That(result.Plan!.Steps, Has.Count.EqualTo(2));
    }

    [Test]
    public void ParseCanUseNaturalSourceAndResultOrder()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult result = context.CompileSurface(
            "PARSE JSON \"post.json\" AS post");

        Assert.That(result.IsValid, Is.True,
            string.Join("; ", result.Diagnostics.Select(item => item.Message)
                .Concat(result.Lowering.Diagnostics.Select(item => item.Message))));
        Assert.That(result.Plan!.Steps.Single().Command.Frame.Id.Value,
            Is.EqualTo("core.parse.json"));
        Assert.That(result.Lowering.CanonicalSyntax.Commands.Single().AllTokens.Select(token => token.Text),
            Is.EqualTo(new[] { "PARSE", "JSON", "[post]", "FROM", "{post.json}" }));
    }

    [Test]
    public void ParseJsonFilesExpandsToNamedIndependentResults()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult result = context.CompileSurface("PARSE JSON FILES \"post.json\", \"todo.json\"");

        Assert.That(result.IsValid, Is.True, string.Join(" | ", result.Lowering.Diagnostics.Select(item => item.Message)));
        Assert.That(result.Plan!.Steps, Has.Count.EqualTo(2));
        Assert.That(result.Lowering.CanonicalSyntax.Commands.Select(command => command.AllTokens[2].Text),
            Is.EqualTo(new[] { "[post]", "[todo]" }));
        Assert.That(result.Lowering.CanonicalSyntax.Links.Single().Kind,
            Is.EqualTo(FluNET.Prompt.CommandLinkKind.Parallel));
    }

    [Test]
    public void CompactErrorPolicyAppliesToFollowingCommands()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var result = context.CompileSurface("ON ERROR CONTINUE\nSAY \"still running\"");

        Assert.That(result.IsValid, Is.True,
            string.Join("; ", result.Lowering.Diagnostics.Select(d => d.Message)));
        Assert.That(result.Plan!.Steps.Single().Policy.ErrorBehavior,
            Is.EqualTo(FluNET.Execution.Workflow.WorkflowErrorBehavior.Continue));
    }

    [Test]
    public void SaySupportsACompactTrailingCondition()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var result = context.CompileSurface("LET enabled = true\nSAY \"ready\" IF [enabled]");

        Assert.That(result.IsValid, Is.True,
            string.Join("; ", result.Lowering.Diagnostics.Select(d => d.Message)));
        Assert.That(result.Plan!.Steps.Last().Policy.Condition,
            Is.EqualTo("[enabled]"));
    }

    [Test]
    public void CompactBlockConnectorsAndPoliciesLowerToCanonicalMetadata()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var result = context.CompileSurface("SAY \"one\"\nWITH RETRY {3} WITH TIMEOUT {30s}\nTHEN\nSAY \"two\"");

        Assert.That(result.IsValid, Is.True,
            string.Join("; ", result.Lowering.Diagnostics.Select(d => d.Message)));
        Assert.That(result.Lowering.CanonicalSyntax.Links.Single().Kind,
            Is.EqualTo(FluNET.Prompt.CommandLinkKind.Sequence));
        Assert.That(result.Plan!.Steps.Last().Policy.RetryCount, Is.EqualTo(3));
        Assert.That(result.Plan.Steps.Last().Policy.Timeout, Is.EqualTo(TimeSpan.FromSeconds(30)));
    }

    [Test]
    public async Task CollectionOperatorsExecuteAgainstTheImplicitPipeline()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(services =>
            services.AddSingleton<IHttpTransport>(new JsonHttp("[{\"id\":1},{\"id\":1},{\"id\":2}]")));

        var result = await context.ExecuteSurfaceAsync(
            "GET https://example.test/posts AS posts | DISTINCT BY id | COUNT");

        Assert.That(result.IsSuccess, Is.True,
            result.Error?.ToString() ?? string.Join("; ", result.Compilation.Diagnostics.Select(d => d.Message)));
        Assert.That(result.Result, Is.EqualTo(2m));
    }

    [Test]
    public void NaturalAliasesAndContextPronounsLowerToTheSamePipeline()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var result = context.CompileSurface(
            "FETCH https://example.test/posts AS posts\nKEEP THE FIRST 5\nWHERE their.active == true\nSAVE THEM AS active.json");

        Assert.That(result.IsValid, Is.True,
            string.Join(" | ", result.Lowering.Diagnostics.Select(item => item.Message)));
        Assert.That(result.Plan!.Steps.Select(step => step.Command.Frame.Id.Value),
            Is.EqualTo(new[] { "surface.get.http.json", "surface.data.take.json", "surface.data.filter.json", "core.save.json" }));
    }

    [Test]
    public void RemoveDuplicatesAndOtherwiseAreNaturalAliases()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var result = context.CompileSurface(
            "GET https://example.test/posts AS posts\nREMOVE DUPLICATES BY id\nSAY \"found\" IF [posts]\nOTHERWISE\nSAY \"empty\"");

        Assert.That(result.IsValid, Is.True,
            string.Join(" | ", result.Diagnostics.Select(item => item.Message)));
        Assert.That(result.Plan!.Steps[1].Command.Frame.Id.Value,
            Is.EqualTo("surface.data.distinct.json"));
    }

    [Test]
    public void NaturalDescendingSortIsAccepted()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var result = context.CompileSurface(
            "GET https://example.test/posts AS posts | ORDER BY createdAt NEWEST");

        Assert.That(result.IsValid, Is.True,
            string.Join(" | ", result.Diagnostics.Select(item => item.Message)));
        Assert.That(result.Plan!.Steps.Last().Command.Frame.Id.Value,
            Is.EqualTo("surface.data.sort.json"));
    }

    private sealed class JsonHttp(string json) : IHttpTransport
    {
        public Task<byte[]> GetBytesAsync(Uri uri, CancellationToken cancellationToken = default) =>
            Task.FromResult(Encoding.UTF8.GetBytes(json));

        public Task<string> PostJsonAsync(Uri uri, string body, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
