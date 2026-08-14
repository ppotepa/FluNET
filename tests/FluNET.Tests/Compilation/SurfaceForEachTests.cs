using FluNET.Compilation;
using FluNET.Context;
using FluNET.Execution.Commands;
using FluNET.Variables;
using System.Text.Json;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class SurfaceForEachTests
{
    [Test]
    public void ForEachBlockCompilesAsOneBoundedIterationCommand()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        const string source = """
GET https://api.example.test/users AS users
FOR EACH user PARALLEL 8
    SAY "{user.name}"
""";
        SurfaceCompilationResult result = context.CompileSurface(source);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True, Diagnostics(result));
            Assert.That(result.Plan!.Steps, Has.Count.EqualTo(2));
            Assert.That(result.Plan.Steps[1].Command.Frame.Id.Value, Is.EqualTo("surface.flow.foreach.json"));
            Assert.That(result.Plan.Steps[1].Dependencies.Select(item => item.PredecessorIndex), Does.Contain(0));
        });
    }

    [Test]
    public async Task HandlerHonorsConfiguredConcurrencyBound()
    {
        using JsonDocument document = JsonDocument.Parse("[{\"id\":1},{\"id\":2},{\"id\":3},{\"id\":4}]");
        JsonElement[] values = document.RootElement.EnumerateArray().Select(item => item.Clone()).ToArray();
        TrackingAction action = new();
        ForEachJsonCommand command = new(new LiteralExpression<JsonElement[]>(values), "item", 2, [action]);
        ForEachJsonCommandHandler handler = new(new EmptyResolver());
        _ = await handler.HandleAsync(command);
        Assert.That(action.MaxObserved, Is.LessThanOrEqualTo(2));
        Assert.That(action.Calls, Is.EqualTo(4));
    }

    private static string Diagnostics(SurfaceCompilationResult result) =>
        string.Join(" | ", result.Lowering.Diagnostics.Select(item => $"{item.Code}: {item.Message}")) + " " +
        string.Join(" | ", result.Diagnostics.Select(item => $"{item.Code}: {item.Message}"));

    private sealed class TrackingAction : IForEachJsonAction
    {
        private int _active;
        private int _max;
        private int _calls;
        public int MaxObserved => _max;
        public int Calls => _calls;
        public async ValueTask ExecuteAsync(IVariableResolver variables, CancellationToken cancellationToken)
        {
            int active = Interlocked.Increment(ref _active);
            int snapshot;
            while ((snapshot = _max) < active && Interlocked.CompareExchange(ref _max, active, snapshot) != snapshot) { }
            Interlocked.Increment(ref _calls);
            try { await Task.Delay(10, cancellationToken); }
            finally { Interlocked.Decrement(ref _active); }
        }
    }

    private sealed class EmptyResolver : IVariableResolver
    {
        public void Register<T>(string name, T value) { }
        public bool IsRegistered(string name) => false;
        public T? Resolve<T>(string tokenValue) => default;
        public void Clear() { }
        public IEnumerable<string> GetVariableNames() => [];
    }
}
