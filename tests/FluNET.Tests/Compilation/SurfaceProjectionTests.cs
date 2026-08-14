using FluNET.Compilation;
using FluNET.Context;
using FluNET.Execution.Commands;
using FluNET.Variables;
using System.Text.Json;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class SurfaceProjectionTests
{
    [Test]
    public void SelectAndMapCompileIntoTheSameProjectFrame()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult select = context.CompileSurface(
            "GET https://api.example.test/posts AS posts | SELECT id, title");
        SurfaceCompilationResult map = context.CompileSurface(
            "GET https://api.example.test/posts AS posts | MAP TO { id, headline: title }");

        Assert.Multiple(() =>
        {
            Assert.That(select.IsValid, Is.True, Diagnostics(select));
            Assert.That(map.IsValid, Is.True, Diagnostics(map));
            Assert.That(select.Plan!.Steps[^1].Command.Frame.Id.Value, Is.EqualTo("surface.data.project.json"));
            Assert.That(map.Plan!.Steps[^1].Command.Frame.Id.Value, Is.EqualTo("surface.data.project.json"));
        });
    }

    [Test]
    public void ProjectionUsesNormalJsonDataExpressions()
    {
        using JsonDocument document = JsonDocument.Parse("{\"id\":7,\"title\":\"Hello\",\"user\":{\"name\":\"Ada\"}}");
        JsonProjection projection = JsonProjection.Map("{ id, headline: title, author: user.name }");
        JsonElement result = projection.Evaluate(document.RootElement, new EmptyResolver());

        Assert.Multiple(() =>
        {
            Assert.That(result.GetProperty("id").GetDecimal(), Is.EqualTo(7m));
            Assert.That(result.GetProperty("headline").GetString(), Is.EqualTo("Hello"));
            Assert.That(result.GetProperty("author").GetString(), Is.EqualTo("Ada"));
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
