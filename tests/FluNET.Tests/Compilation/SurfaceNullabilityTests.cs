using FluNET.Compilation;
using FluNET.Context;
using FluNET.Execution.Commands;
using FluNET.Prompt.Expressions;
using FluNET.Variables;
using System.Text.Json;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class SurfaceNullabilityTests
{
    [Test]
    public void CoalesceIsRightAssociativeAndAvailableToDataExpressions()
    {
        ExpressionSyntax syntax = ExpressionSyntaxParser.Parse("missing ?? fallback ?? 8080");
        Assert.That(syntax, Is.TypeOf<BinaryExpressionSyntax>());
        BinaryExpressionSyntax root = (BinaryExpressionSyntax)syntax;
        Assert.That(root.Operator, Is.EqualTo("??"));
        Assert.That(root.Right, Is.TypeOf<BinaryExpressionSyntax>());

        using JsonDocument document = JsonDocument.Parse("{\"fallback\":42}");
        object? value = JsonDataExpression.Parse("missing ?? fallback ?? 8080")
            .Evaluate(document.RootElement, new EmptyResolver());
        Assert.That(value, Is.EqualTo(42m));
    }

    [Test]
    public void DefaultStageCompilesAsTypedJsonTransform()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult result = context.CompileSurface(
            "GET https://api.example.test/posts AS posts | DEFAULT title TO \"unknown\" | MAP TO { title: title ?? \"unknown\" }");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True, Diagnostics(result));
            Assert.That(result.Plan!.Steps.Select(step => step.Command.Frame.Id.Value), Does.Contain("surface.data.default.json"));
            Assert.That(result.Plan.Steps[^1].Command.Frame.Id.Value, Is.EqualTo("surface.data.project.json"));
        });
    }

    [Test]
    public void DefaultOnlyFillsMissingOrNullFields()
    {
        JsonDefaultSpec spec = JsonDefaultSpec.Parse("name|\"unknown\"");
        using JsonDocument missing = JsonDocument.Parse("{\"id\":1}");
        using JsonDocument present = JsonDocument.Parse("{\"name\":\"Ada\"}");
        JsonElement a = spec.Apply(missing.RootElement, new EmptyResolver());
        JsonElement b = spec.Apply(present.RootElement, new EmptyResolver());
        Assert.Multiple(() =>
        {
            Assert.That(a.GetProperty("name").GetString(), Is.EqualTo("unknown"));
            Assert.That(b.GetProperty("name").GetString(), Is.EqualTo("Ada"));
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
