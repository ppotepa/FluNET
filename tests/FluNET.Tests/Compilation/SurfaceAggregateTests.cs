using FluNET.Compilation;
using FluNET.Context;
using FluNET.Execution.Commands;
using FluNET.Variables;
using System.Text.Json;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class SurfaceAggregateTests
{
    [Test]
    public void GroupAndSumCompileAsTypedDataStages()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult group = context.CompileSurface("GET https://api.example.test/orders AS orders | GROUP BY customerId");
        SurfaceCompilationResult sum = context.CompileSurface("GET https://api.example.test/orders AS orders | SUM total AS revenue");
        Assert.Multiple(() =>
        {
            Assert.That(group.IsValid, Is.True, Diagnostics(group));
            Assert.That(sum.IsValid, Is.True, Diagnostics(sum));
            Assert.That(group.Plan!.Steps[^1].Command.Frame.Id.Value, Is.EqualTo("surface.data.group.json"));
            Assert.That(sum.Plan!.Steps[^1].Command.Frame.Id.Value, Is.EqualTo("surface.data.sum.json"));
        });
    }

    [Test]
    public void JoinAndMatchLowerToSameTypedJoinFrame()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        const string joinSource = """
GET https://api.example.test/posts AS posts
GET https://api.example.test/users AS users
JOIN posts WITH users ON posts.userId = users.id
""";
        const string matchSource = """
GET https://api.example.test/posts AS posts
GET https://api.example.test/users AS users
MATCH posts.userId TO users.id
""";
        SurfaceCompilationResult join = context.CompileSurface(joinSource);
        SurfaceCompilationResult match = context.CompileSurface(matchSource);
        Assert.Multiple(() =>
        {
            Assert.That(join.IsValid, Is.True, Diagnostics(join));
            Assert.That(match.IsValid, Is.True, Diagnostics(match));
            Assert.That(join.Plan!.Steps[^1].Command.Frame.Id.Value, Is.EqualTo("surface.data.join.json"));
            Assert.That(match.Plan!.Steps[^1].Command.Frame.Id.Value, Is.EqualTo("surface.data.join.json"));
            Assert.That(join.Plan.Steps[^1].Dependencies.Select(item => item.PredecessorIndex), Is.EquivalentTo(new[] { 0, 1 }));
        });
    }

    [Test]
    public async Task SumAndJoinHandlersProduceDeterministicValues()
    {
        using JsonDocument ordersDoc = JsonDocument.Parse("[{\"customerId\":1,\"total\":2},{\"customerId\":1,\"total\":3}]");
        JsonElement[] orders = ordersDoc.RootElement.EnumerateArray().Select(item => item.Clone()).ToArray();
        EmptyResolver variables = new();
        decimal sum = await new SumJsonCommandHandler(variables).HandleAsync(
            new SumJsonCommand(new LiteralExpression<JsonElement[]>(orders), JsonDataExpression.Parse("total")));
        Assert.That(sum, Is.EqualTo(5m));
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
