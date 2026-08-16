using FluNET.Context;
using FluNET.Compilation;
using FluNET.Execution.Planning;
using FluNET.Prompt;
using FluNET.Prompt.Surface;
using FluNET.Tooling;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class SentencePipelineTests
{
    [Test]
    public void ProcessedPromptExposesSourceSentencesBeforeSyntax()
    {
        ProcessedPrompt prompt = new(
            "GET [users] FROM {users.json}.\n  FILTER active == true; SAY \"done\"");

        Assert.That(prompt.Sentences.Select(sentence => sentence.Text),
            Is.EqualTo(new[]
            {
                "GET [users] FROM {users.json}",
                "FILTER active == true",
                "SAY \"done\""
            }));
        Assert.That(prompt.Sentences[1].Indentation, Is.EqualTo(2));
        Assert.That(prompt.Sentences[0].Span.Start, Is.EqualTo(0));
    }

    [Test]
    public void SurfaceDocumentPreservesSentenceToStatementSpans()
    {
        SourceDocument document = new(
            "GET https://example.test/posts AS posts\nWHERE active == true");

        Assert.That(document.Sentences, Has.Count.EqualTo(2));
        SurfaceParseResult parsed = new SurfaceParser().Parse(document);

        Assert.That(parsed.IsValid, Is.True,
            string.Join(" | ", parsed.Diagnostics.Select(item => item.Message)));
        Assert.That(parsed.Program.Statements, Has.Count.EqualTo(2));
        Assert.That(parsed.Program.Statements.Select(statement => statement.SentenceIndex),
            Is.EqualTo(new[] { 0, 1 }));
        Assert.That(parsed.Program.Statements[0].Span.Start,
            Is.EqualTo(document.Sentences[0].Span.Start));
        Assert.That(parsed.Sentences, Is.SameAs(document.Sentences));
        Assert.That(document.FindSentence(parsed.Program.Statements[1].Span)!.Index, Is.EqualTo(1));
    }

    [Test]
    public void SurfaceDocumentMapsSyntheticForEachCommandToItsHeaderSentence()
    {
        SourceDocument document = new(
            "GET https://example.test/users AS users\nFOR EACH user IN users\n    SAY \"{user.name}\"");

        SurfaceParseResult parsed = new SurfaceParser().Parse(document);

        Assert.That(parsed.IsValid, Is.True,
            string.Join(" | ", parsed.Diagnostics.Select(item => item.Message)));
        Assert.That(parsed.Program.Statements, Has.Count.EqualTo(2));
        Assert.That(parsed.Program.Statements[0].SentenceIndex, Is.EqualTo(0));
        Assert.That(parsed.Program.Statements[1].SentenceIndex, Is.EqualTo(1));
        Assert.That(document.FindSentence(parsed.Program.Statements[1].Span)!.Text,
            Is.EqualTo("FOR EACH user IN users"));
    }

    [Test]
    public void GraphShowsSentenceCompilerAndExecutorStages()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult compilation = context.CompileSurface(
            "GET https://example.test/posts AS posts\nWHERE active == true");

        Assert.That(compilation.IsValid, Is.True,
            string.Join(" | ", compilation.Diagnostics.Select(item => item.Message)));
        string graph = new SurfaceGraphExporter().ToDot(compilation);

        Assert.That(graph, Does.Contain("SentenceSegmenter"));
        Assert.That(graph, Does.Contain("SurfaceLowerer"));
        Assert.That(graph, Does.Contain("ExecutionPlanner"));
        Assert.That(graph, Does.Contain("SentenceExecutor"));
        Assert.That(graph, Does.Contain("Sentence 0"));
        Assert.That(graph, Does.Contain("Capability / ProviderResolver"));
    }

    [Test]
public void ContextResolvesSentenceExecutor()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();

        Assert.That(context.GetService<SentenceExecutor>(), Is.Not.Null);
}

[Test]
public void ScanFilesIsAFirstClassPortableSurfaceCapability()
{
    using FluNETContext context = FluNETContext.Create();
    SurfaceCompilationResult compilation = context.CompileSurface(
        "SCAN \"./data/*.json\" AS files\nSAY \"found\" IF [files]");

    if (!compilation.IsValid)
        Assert.Fail(string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => item.Message)));
    Assert.That(compilation.Plan!.Steps[0].Command.Frame.Id.Value,
        Is.EqualTo("surface.files.scan.json"));
}

[Test]
public void FindFilesUsesTheSameCapabilityWithRecursiveIntent()
{
    using FluNETContext context = FluNETContext.Create();
    SurfaceCompilationResult compilation = context.CompileSurface(
        "FIND \"./data/*.json\" AS files");
    Assert.That(compilation.IsValid, Is.True,
        string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => item.Message)));
    Assert.That(compilation.Plan!.Steps[0].Command.Frame.Id.Value,
        Is.EqualTo("surface.files.scan.json"));
}

[Test]
public void HashFilesLowersToAReadOnlyCapability()
{
    using FluNETContext context = FluNETContext.Create();
    SurfaceCompilationResult compilation = context.CompileSurface(
        "HASH \"./data.json\" AS digest");

    Assert.That(compilation.IsValid, Is.True,
        string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => item.Message)));
    Assert.That(compilation.Plan!.Steps[0].Command.Frame.Id.Value,
        Is.EqualTo("surface.files.hash"));
}

[Test]
    public void GraphNamesTheFileCapabilityUsedByThePlan()
{
    using FluNETContext context = FluNETContext.Create();
    SurfaceCompilationResult compilation = context.CompileSurface(
        "SCAN \"./data/*.json\" AS files");

    string graph = new SurfaceGraphExporter().ToDot(compilation);

        Assert.That(graph, Does.Contain("filesystem.scan"));
    }

    [Test]
    public void GraphNamesTheConfiguredDatabaseCapability()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult compilation = context.CompileSurface(
            "GET sql:\"SELECT id FROM items\" AS items");

        Assert.That(compilation.IsValid, Is.True,
            string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => item.Message)));
        Assert.That(new SurfaceGraphExporter().ToDot(compilation), Does.Contain("database.sql"));
    }

    [Test]
    public void ExecuteSugarLowersToThePortableProcessCapability()
    {
        using FluNETContext context = FluNETContext.Create();
        SurfaceCompilationResult compilation = context.CompileSurface(
            "EXECUTE \"dotnet --version\" AS result");

        Assert.That(compilation.IsValid, Is.True,
            string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => item.Message)));
        Assert.That(compilation.Plan!.Steps[0].Command.Frame.Id.Value,
            Is.EqualTo("system.process.run"));
        Assert.That(new SurfaceGraphExporter().ToDot(compilation), Does.Contain("system.process"));
    }

    [Test]
    public void ArchiveSugarLowersToTheFilesystemArchiveCapability()
    {
        using FluNETContext context = FluNETContext.Create();
        SurfaceCompilationResult compilation = context.CompileSurface(
            "PACK \"./input.txt\" TO \"./bundle.zip\" AS archive");

        Assert.That(compilation.IsValid, Is.True,
            string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => item.Message)));
        Assert.That(compilation.Plan!.Steps[0].Command.Frame.Id.Value,
            Is.EqualTo("filesystem.archive.create"));
        Assert.That(new SurfaceGraphExporter().ToDot(compilation), Does.Contain("filesystem.archive"));
    }

    [Test]
    public void MakeDirectorySugarLowersToTheDirectoryCapability()
    {
        using FluNETContext context = FluNETContext.Create();
        SurfaceCompilationResult compilation = context.CompileSurface(
            "MKDIR \"./reports\" AS directory");

        Assert.That(compilation.IsValid, Is.True,
            string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => item.Message)));
        Assert.That(compilation.Plan!.Steps[0].Command.Frame.Id.Value,
            Is.EqualTo("filesystem.directory.create"));
        Assert.That(new SurfaceGraphExporter().ToDot(compilation), Does.Contain("filesystem.directory"));
    }

    [Test]
    public async Task TopLevelSequenceExecutesDependentDirectoryCommands()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-sequence-" + Guid.NewGuid().ToString("N"));
        string first = Path.Combine(root, "first");
        string second = Path.Combine(root, "second");
        try
        {
            using FluNETContext context = FluNETContext.Create();
            SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync($"MKDIR \"{first}\" AS first\nTHEN\nMKDIR \"{second}\" AS second");

            Assert.That(
                execution.IsSuccess,
                Is.True,
                string.Join(
                    Environment.NewLine,
                    execution.Compilation.Diagnostics.Select(item => item.Code + ": " + item.Message)) +
                Environment.NewLine +
                execution.Error?.ToString());
            Assert.That(Directory.Exists(first), Is.True);
            Assert.That(Directory.Exists(second), Is.True);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void SequenceBeforeExpandedPipelineLinksToItsFirstLoweredStep()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult compilation = context.CompileSurface(
            "MKDIR \"./sequence-root\" AS root\nTHEN\nGET https://jsonplaceholder.typicode.com/users AS items | FILTER id > 0 AS filtered");

        if (!compilation.IsValid)
            Assert.Fail(string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => item.Code + ": " + item.Message)) + "\n" +
                string.Join(Environment.NewLine, compilation.Lowering.Diagnostics.Select(item => item.Code + ": " + item.Message)));
        Assert.That(compilation.Plan!.Steps[1].Dependencies,
            Has.Some.Matches<ExecutionDependency>(dependency =>
                dependency.PredecessorIndex == 0 && dependency.Kind == ExecutionDependencyKind.Sequence));
    }

    [Test]
    public void NaturalGetFromSyntaxLowersToTheSameHttpFrame()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult compilation = context.CompileSurface(
            "GET users FROM https://api.example.test/users.");

        Assert.That(compilation.IsValid, Is.True,
            string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => item.Code + ": " + item.Message)) + "\n" +
            string.Join(Environment.NewLine, compilation.Lowering.Diagnostics.Select(item => item.Code + ": " + item.Message)));
        Assert.That(compilation.Plan!.Steps.Single().Command.Frame.Id.Value,
            Is.EqualTo("surface.get.http.json"));
        Assert.That(compilation.Plan.Steps.Single().ResultBinding!.Targets,
            Is.EquivalentTo(new[] { "users" }));
    }

    [Test]
    public void NaturalAggregateSyntaxBindsToTheNamedCollection()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult compilation = context.CompileSurface(
            "GET https://api.example.test/users AS users\nCOUNT users AS total.");

        Assert.That(compilation.IsValid, Is.True,
            string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => item.Code + ": " + item.Message)));
        Assert.That(compilation.Plan!.Steps[1].Dependencies.Select(item => item.PredecessorIndex),
            Is.EquivalentTo(new[] { 0 }));
    }

    [Test]
    public void PeriodSeparatesMultipleNaturalSentencesOnOneLine()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult compilation = context.CompileSurface(
            "SAY one. SAY two.");

        Assert.That(compilation.IsValid, Is.True,
            string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => item.Code + ": " + item.Message)));
        Assert.That(compilation.Plan!.Steps, Has.Count.EqualTo(2));
        Assert.That(compilation.Document.Sentences, Has.Count.EqualTo(2));
    }

    [Test]
    public void RepeatBlockExpandsIntoOrderedPlanSteps()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult compilation = context.CompileSurface(
            "REPEAT 3 TIMES:\n    SAY \"tick\"");

        Assert.That(compilation.IsValid, Is.True,
            string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => item.Code + ": " + item.Message)));
        Assert.That(compilation.Plan!.Steps, Has.Count.EqualTo(3));
        Assert.That(compilation.Plan.Steps[1].Dependencies.Select(item => item.PredecessorIndex),
            Is.EquivalentTo(new[] { 0 }));
        Assert.That(compilation.Plan.Steps[2].Dependencies.Select(item => item.PredecessorIndex),
            Is.EquivalentTo(new[] { 1 }));
    }

    [TestCase("TRIM \" text \" AS clean", "surface.text.trim")]
    [TestCase("UPPER \"text\" AS clean", "surface.text.upper")]
    [TestCase("LOWER \"TEXT\" AS clean", "surface.text.lower")]
    [TestCase("SPLIT \"a,b\" BY \",\" AS parts", "surface.text.split")]
    [TestCase("LINES \"a\\nb\" AS lines", "surface.text.lines")]
    public void TextSugarLowersToTypedTextFrames(string source, string frame)
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult compilation = context.CompileSurface(source);

        if (!compilation.IsValid)
            Assert.Fail(string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => item.Code + ": " + item.Message)) + "\n" +
                string.Join(Environment.NewLine, compilation.Lowering.Diagnostics.Select(item => item.Code + ": " + item.Message)));
        Assert.That(compilation.Plan!.Steps.Last().Command.Frame.Id.Value, Is.EqualTo(frame));
    }

    [Test]
    public void CombineSugarConsumesSplitText()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult compilation = context.CompileSurface(
            "SPLIT \"a,b\" BY \",\" AS parts\nTHEN\nCOMBINE [parts] WITH \",\" AS joined");

        Assert.That(compilation.IsValid, Is.True,
            string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => item.Code + ": " + item.Message)));
        Assert.That(compilation.Plan!.Steps.Last().Command.Frame.Id.Value, Is.EqualTo("surface.text.join"));
    }

    [Test]
    public async Task TrimTextExecutesAsARegularSentence()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync("TRIM \"  hello  \" AS clean");

        Assert.That(execution.IsSuccess, Is.True,
            execution.Error?.ToString() + " " +
            string.Join(" | ", execution.Compilation.Diagnostics.Select(item => item.Code + ":" + item.Message)) + " " +
            string.Join(" | ", execution.Compilation.Lowering.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        Assert.That(execution.Result, Is.EqualTo("hello"));
    }

    [Test]
    public async Task ExpectTextValidatesAReadableAssertion()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(
            "EXPECT \"health: ok\" TO CONTAIN \"ok\" AS verified");

        Assert.That(execution.IsSuccess, Is.True, execution.Error?.ToString());
        Assert.That(execution.Result, Is.EqualTo(true));
    }

    [Test]
    public async Task ExpectTextFailsWithAnActionableMessage()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(
            "EXPECT \"health: failed\" TO EQUAL \"health: ok\"");

        Assert.That(execution.IsSuccess, Is.False);
        Assert.That(execution.Error?.Message, Does.Contain("Expectation failed"));
    }

    [Test]
    public async Task MetricsExposePortableProcessInformation()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync("METRICS AS metrics");

        Assert.That(execution.IsSuccess, Is.True, execution.Error?.ToString());
        Assert.That(execution.Result, Is.TypeOf<System.Text.Json.JsonElement>());
        Assert.That(((System.Text.Json.JsonElement)execution.Result!).GetProperty("processId").GetInt32(), Is.GreaterThan(0));
    }

    [TestCase("FIND \"./\" WHERE size > 10MB AS large")]
    [TestCase("FIND \"./\" WHERE length >= 1MiB AS large")]
    public void FilePredicatesAcceptPortableSizeUnits(string source)
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult compilation = context.CompileSurface(source);

        Assert.That(compilation.IsValid, Is.True,
            string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => item.Code + ": " + item.Message)));
    }

    [Test]
    public void RequestProducesAnInspectableHttpResponseEnvelope()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult compilation = context.CompileSurface(
            "REQUEST https://api.example.test/health AS response\nEXPECT response.status TO EQUAL \"200\"");

        Assert.That(compilation.IsValid, Is.True,
            string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => item.Code + ": " + item.Message)));
        Assert.That(compilation.Plan!.Steps[0].Command.Frame.Id.Value, Is.EqualTo("network.http.response"));
        Assert.That(compilation.Plan.Steps[1].Command.Frame.Id.Value, Is.EqualTo("surface.text.expect"));
    }

    [Test]
    public async Task RequestPreservesNonSuccessStatusForExpectations()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(services =>
            services.AddSingleton<FluNET.Capabilities.IHttpTransport>(new ResponseHttpTransport()));
        SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(
            "REQUEST https://api.example.test/missing AS response\nEXPECT response.status TO EQUAL \"404\"");

        Assert.That(execution.IsSuccess, Is.True, execution.Error?.ToString());
        Assert.That(execution.Result, Is.EqualTo(true));
    }

    [Test]
    public async Task WhileEvaluatesItsConditionAtRuntimeAndCanSkipItsBody()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(
            "WHILE false MAX 3:\n    SAY \"should not print\"");

        Assert.That(execution.IsSuccess, Is.True,
            execution.Error?.ToString() + " " +
            string.Join(" | ", execution.Compilation.Diagnostics.Select(item => item.Code + ":" + item.Message)) + " " +
            string.Join(" | ", execution.Compilation.Lowering.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        Assert.That(execution.Result, Is.EqualTo(true));
    }

    [Test]
    public async Task WhileCanMutateAValueUntilItsConditionBecomesFalse()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(
            "LET counter = 0\nWHILE counter < 3 MAX 10:\n    INCREMENT [counter]\nEXPECT \"{counter}\" TO EQUAL \"3\"");

        Assert.That(execution.IsSuccess, Is.True,
            execution.Error?.ToString() + " " +
            string.Join(" | ", execution.Compilation.Diagnostics.Select(item => item.Code + ":" + item.Message)) + " " +
            string.Join(" | ", execution.Compilation.Lowering.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        Assert.That(execution.Result, Is.EqualTo(true));
    }

    [Test]
    public void IfAndElseBlocksUseNaturalConditions()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult result = context.CompileSurface(
            "LET enabled = true\nIF enabled:\n    SAY \"ready\"\nELSE:\n    SAY \"not ready\"");

        Assert.That(result.IsValid, Is.True,
            string.Join(" | ", result.SurfaceParse.Diagnostics.Select(item => item.Code + ":" + item.Message)) + " " +
            string.Join(" | ", result.Lowering.Diagnostics.Select(item => item.Code + ":" + item.Message)) + " " +
            string.Join(" | ", result.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        Assert.That(result.Plan!.Steps, Has.Count.EqualTo(3));
        Assert.That(result.Plan.Steps[1].Policy.Condition, Does.Contain("[enabled]"));
        Assert.That(result.Plan.Steps[2].Policy.Condition, Does.Contain("NOT"));
    }

    [Test]
    public async Task WhileSupportsConditionalBreak()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(
            "SET attempts TO 0\nWHILE attempts < 10 MAX 20:\n    INCREMENT [attempts]\n    CONTINUE WHEN attempts == 2\n    BREAK WHEN attempts == 3\nEXPECT \"{attempts}\" TO EQUAL \"3\"");

        Assert.That(execution.IsSuccess, Is.True,
            execution.Error?.ToString() + " " +
            string.Join(" | ", execution.Compilation.Diagnostics.Select(item => item.Code + ":" + item.Message)) + " " +
            string.Join(" | ", execution.Compilation.Lowering.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        Assert.That(execution.Result, Is.EqualTo(true));
    }

    [Test]
    public void ElseIfBlocksComposeIntoExclusivePlanBranches()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult result = context.CompileSurface(
            "LET state = \"starting\"\nIF state == \"ready\":\n    SAY \"ready\"\nELSE IF state == \"starting\":\n    SAY \"starting\"\nELSE:\n    SAY \"failed\"");

        Assert.That(result.IsValid, Is.True,
            string.Join(" | ", result.SurfaceParse.Diagnostics.Select(item => item.Code + ":" + item.Message)) + " " +
            string.Join(" | ", result.Lowering.Diagnostics.Select(item => item.Code + ":" + item.Message)) + " " +
            string.Join(" | ", result.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        Assert.That(result.Plan!.Steps, Has.Count.EqualTo(4));
        Assert.That(result.Plan.Steps[1].Policy.Condition, Does.Contain("[state]"));
        Assert.That(result.Plan.Steps[2].Policy.Condition, Does.Contain("NOT"));
        Assert.That(result.Plan.Steps[3].Policy.Condition, Does.Contain("NOT"));
    }

    [TestCase("SET message TO ready")]
    [TestCase("SET [message] TO ready")]
    public void ConversationalSetSugarLowersToTypedAssignment(string source)
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult result = context.CompileSurface(source);

        Assert.That(result.IsValid, Is.True,
            string.Join(" | ", result.SurfaceParse.Diagnostics.Select(item => item.Code + ":" + item.Message)) + " " +
            string.Join(" | ", result.Lowering.Diagnostics.Select(item => item.Code + ":" + item.Message)) + " " +
            string.Join(" | ", result.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        Assert.That(result.Plan!.Steps, Has.Count.EqualTo(1));
        Assert.That(result.Plan.Steps[0].Command.Frame.Id.Value, Is.EqualTo("core.set.text"));
    }

    [Test]
    public void UnlessIsNegatedIfSugar()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult result = context.CompileSurface(
            "LET ready = false\nUNLESS ready:\n    SAY \"waiting\"");

        Assert.That(result.IsValid, Is.True,
            string.Join(" | ", result.SurfaceParse.Diagnostics.Select(item => item.Code + ":" + item.Message)) + " " +
            string.Join(" | ", result.Lowering.Diagnostics.Select(item => item.Code + ":" + item.Message)) + " " +
            string.Join(" | ", result.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        Assert.That(result.Plan!.Steps[1].Policy.Condition, Does.Contain("NOT"));
    }

    private sealed class ResponseHttpTransport : FluNET.Capabilities.IHttpTransport
    {
        public Task<byte[]> GetBytesAsync(Uri uri, CancellationToken cancellationToken = default) =>
            Task.FromResult(Array.Empty<byte>());

        public Task<FluNET.Capabilities.HttpResourceResponse> GetResponseAsync(Uri uri, CancellationToken cancellationToken = default) =>
            Task.FromResult(new FluNET.Capabilities.HttpResourceResponse(
                "{\"error\":\"missing\"}"u8.ToArray(),
                404,
                "application/json",
                "utf-8",
                new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)));

        public Task<string> PostJsonAsync(Uri uri, string json, CancellationToken cancellationToken = default) =>
            Task.FromResult(string.Empty);
    }

    [Test]
    public void ImportExpandsModulesRelativeToTheRootDocument()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string module = Path.Combine(root, "common.flu");
        string program = Path.Combine(root, "program.flu");
        try
        {
            File.WriteAllText(module, "SAY \"from module\"");
            File.WriteAllText(program, "IMPORT \"common.flu\"\nSAY \"from root\"");
            using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
            SurfaceCompilationResult compilation = context.GetSurfaceCompiler().Compile(
                new SourceDocument(File.ReadAllText(program), SourceSyntaxKind.Auto, program));

            Assert.That(compilation.IsValid, Is.True,
                string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => item.Message)));
            Assert.That(compilation.Plan!.Steps, Has.Count.EqualTo(2));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void ImportCyclesProduceAnActionableDiagnostic()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-import-cycle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string first = Path.Combine(root, "first.flu");
        string second = Path.Combine(root, "second.flu");
        try
        {
            File.WriteAllText(first, "IMPORT \"second.flu\"");
            File.WriteAllText(second, "IMPORT \"first.flu\"");
            using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
            SurfaceCompilationResult compilation = context.GetSurfaceCompiler().Compile(
                new SourceDocument(File.ReadAllText(first), SourceSyntaxKind.Auto, first));

            Assert.That(compilation.IsValid, Is.False);
            Assert.That(compilation.SurfaceParse.Diagnostics.Select(item => item.Code), Does.Contain("FLN384"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void ReplaceSugarCarriesBothTextOperands()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult compilation = context.CompileSurface("REPLACE \"old\" WITH \"new\" IN \"old value\" AS changed");

        if (!compilation.IsValid)
            Assert.Fail(string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => item.Code + ": " + item.Message)) + "\n" +
                string.Join(Environment.NewLine, compilation.Lowering.Diagnostics.Select(item => item.Code + ": " + item.Message)));
        Assert.That(compilation.Plan!.Steps.Last().Command.Frame.Id.Value, Is.EqualTo("surface.text.replace"));
    }

[Test]
public void SystemInfoUsesPortableSystemCapability()
{
    using FluNETContext context = FluNETContext.Create();
    SurfaceCompilationResult compilation = context.CompileSurface(
        "SYSTEM INFO AS system");

    Assert.That(compilation.IsValid, Is.True,
        string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => item.Message)));
    Assert.That(compilation.Plan!.Steps[0].Command.Frame.Id.Value,
        Is.EqualTo("surface.system.info"));
}

[Test]
public async Task SystemInfoExecutesThroughTheExecutorAndProvider()
{
    using FluNETContext context = FluNETContext.Create();
    SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(
        "SYSTEM INFO AS system");

    Assert.That(execution.IsSuccess, Is.True, execution.Error?.ToString());
    Assert.That(execution.Result, Is.TypeOf<System.Text.Json.JsonElement>());
}

[Test]
public void CopyAndMoveExposeOrderedWriteCapabilities()
{
    using FluNETContext context = FluNETContext.Create();
    SurfaceCompilationResult copy = context.CompileSurface(
        "COPY \"./source.txt\" TO \"./backup/source.txt\" AS backup");
    SurfaceCompilationResult move = context.CompileSurface(
        "MOVE \"./source.txt\" TO \"./processed/source.txt\" AS moved");

    Assert.That(copy.IsValid, Is.True,
        string.Join(Environment.NewLine, copy.Diagnostics.Select(item => item.Message)));
    Assert.That(move.IsValid, Is.True,
        string.Join(Environment.NewLine, move.Diagnostics.Select(item => item.Message)));
    Assert.That(copy.Plan!.Steps[0].Command.Frame.Id.Value,
        Is.EqualTo("surface.files.copy"));
    Assert.That(move.Plan!.Steps[0].Command.Frame.Id.Value,
        Is.EqualTo("surface.files.move"));
}

[Test]
public void TrashLowersToRecoverableFileCapability()
{
    using FluNETContext context = FluNETContext.Create();
    SurfaceCompilationResult compilation = context.CompileSurface(
        "TRASH \"./old.txt\" AS removed");

    Assert.That(compilation.IsValid, Is.True,
        string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => item.Message)));
    Assert.That(compilation.Plan!.Steps[0].Command.Frame.Id.Value,
        Is.EqualTo("surface.files.trash"));
}

[Test]
public void StorageSugarLowersToKeyValueCapability()
{
    using FluNETContext context = FluNETContext.Create();
    SurfaceCompilationResult compilation = context.CompileSurface(
        "STORE \"theme\" = \"dark\" AS stored\nREAD \"theme\" AS value");

    Assert.That(compilation.IsValid, Is.True,
        string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => item.Message)));
    Assert.That(compilation.Plan!.Steps[0].Command.Frame.Id.Value,
        Is.EqualTo("storage.put.value"));
    Assert.That(compilation.Plan.Steps[1].Command.Frame.Id.Value,
        Is.EqualTo("storage.read.value"));
}

[Test]
public async Task StorageSugarPersistsValuesWithinAContext()
{
    using FluNETContext context = FluNETContext.Create();
    SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(
        "STORE \"theme\" = \"dark\" AS stored\nREAD \"theme\" AS value");

    Assert.That(execution.IsSuccess, Is.True, execution.Error?.ToString());
    Assert.That(execution.Result, Is.EqualTo("dark"));
}
}
