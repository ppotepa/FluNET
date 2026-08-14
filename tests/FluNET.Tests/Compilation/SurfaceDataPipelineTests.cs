using FluNET.Compilation;
using FluNET.Context;
using FluNET.Execution.Commands;
using FluNET.Language;
using FluNET.Language.Values;
using FluNET.Variables;
using System.Text.Json;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class SurfaceDataPipelineTests
{
    [Test]
    public void FilterSortTakePipelineCompilesAsTypedListJsonChain()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        const string source = "GET https://api.example.test/posts AS posts | FILTER userId == 1 | SORT BY title | TAKE 10";
        SurfaceCompilationResult result = context.CompileSurface(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True, Diagnostics(result));
            Assert.That(result.Plan!.Steps, Has.Count.EqualTo(4));
            Assert.That(result.Plan.Steps.Select(step => step.Command.Frame.Id.Value), Is.EqualTo(new[]
            {
                "surface.get.http.json", "surface.data.filter.json", "surface.data.sort.json", "surface.data.take.json"
            }));
            Assert.That(result.Plan.Steps[1].Dependencies.Select(item => item.PredecessorIndex), Is.EquivalentTo(new[] { 0 }));
            Assert.That(result.Plan.Steps[2].Dependencies.Select(item => item.PredecessorIndex), Is.EquivalentTo(new[] { 1 }));
            Assert.That(result.Plan.Steps[3].Dependencies.Select(item => item.PredecessorIndex), Is.EquivalentTo(new[] { 2 }));
        });
    }

    [Test]
    public void MultilineDataStagesUseSameImplicitPipelineState()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        const string source = """
GET https://api.example.test/posts AS posts
FILTER userId == 1
SORT BY title
TAKE 5
""";
        SurfaceCompilationResult result = context.CompileSurface(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True, Diagnostics(result));
            Assert.That(result.Lowering.CanonicalSyntax.Commands.Select(command => command.Verb.Text),
                Is.EqualTo(new[] { "GETHTTP", "FILTERJSON", "SORTJSON", "TAKEJSON" }));
            Assert.That(result.Lowering.InferenceTrace.Items.Count(item => item.Rule == "synthetic-pipeline-output"), Is.EqualTo(3));
        });
    }

    [Test]
    public void HttpJsonToJsonListIsAnExplicitRuntimeCheckedConversion()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        IValueCodecRegistry values = context.GetService<IValueCodecRegistry>();
        LanguageSnapshot language = context.GetService<LanguageSnapshot>();
        ConversionResolution resolution = values.ResolveConversion(language.Types.Json, language.Types.List(language.Types.Json));
        using JsonDocument arrayDocument = JsonDocument.Parse("[{\"id\":1},{\"id\":2}]");
        using JsonDocument objectDocument = JsonDocument.Parse("{\"id\":1}");
        object converted = values.Convert(arrayDocument.RootElement.Clone(), resolution.Path!);

        Assert.Multiple(() =>
        {
            Assert.That(resolution.IsFound, Is.True);
            Assert.That(converted, Is.TypeOf<JsonElement[]>());
            Assert.That((JsonElement[])converted, Has.Length.EqualTo(2));
            Assert.That(() => values.Convert(objectDocument.RootElement.Clone(), resolution.Path!), Throws.TypeOf<FormatException>());
        });
    }

    [Test]
    public void JsonDataExpressionEvaluatesRowFieldsWithNormalExpressionPrecedence()
    {
        using JsonDocument document = JsonDocument.Parse("{\"userId\":1,\"title\":\"Alpha\",\"score\":4}");
        JsonDataExpression predicate = JsonDataExpression.Parse("userId == 1 AND score * 2 >= 8");
        JsonDataExpression key = JsonDataExpression.Parse("title");
        EmptyResolver variables = new();

        Assert.Multiple(() =>
        {
            Assert.That(predicate.EvaluateBoolean(document.RootElement, variables), Is.True);
            Assert.That(key.Evaluate(document.RootElement, variables), Is.EqualTo("Alpha"));
        });
    }

    private static string Diagnostics(SurfaceCompilationResult result) =>
        string.Join(" | ", result.Lowering.Diagnostics.Select(item => $"{item.Code}: {item.Message}")) + " " +
        string.Join(" | ", result.Diagnostics.Select(item => $"{item.Code}: {item.Message}"));

    private sealed class EmptyResolver : IVariableResolver
    {
        public void Register<T>(string name, T value) { }
        public bool IsRegistered(string name) => false;
        public T? Resolve<T>(string tokenValue) => default;
        public void Clear() { }
        public IEnumerable<string> GetVariableNames() => [];
    }
}
