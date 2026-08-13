using FluNET.Execution.Commands;
using FluNET.Prompt.Expressions;
using FluNET.Variables;

namespace FluNET.Tests.Execution;

[TestFixture]
public sealed class ConditionExpressionCompilerTests
{
    [Test]
    public void CompilerEvaluatesBooleanComparisonTreeAndCollectsDependencies()
    {
        ExpressionSyntax syntax = ExpressionSyntaxParser.Parse(
            "([enabled] AND NOT [blocked]) OR [count] >= 3");
        CompiledCondition condition = new ConditionExpressionCompiler().Compile(syntax);
        DictionaryVariableResolver variables = new(new Dictionary<string, object?>
        {
            ["enabled"] = true,
            ["blocked"] = false,
            ["count"] = 4m
        });

        bool value = condition.Expression.Evaluate(new EvaluationContext(variables));

        Assert.Multiple(() =>
        {
            Assert.That(value, Is.True);
            Assert.That(condition.VariableReferences,
                Is.EquivalentTo(new[] { "enabled", "blocked", "count" }));
        });
    }

    [Test]
    public void CompilerEvaluatesStructuredObjectAccess()
    {
        ExpressionSyntax syntax = ExpressionSyntaxParser.Parse(
            "OBJECT(enabled: true, count: 2).enabled");
        CompiledCondition condition = new ConditionExpressionCompiler().Compile(syntax);

        bool value = condition.Expression.Evaluate(
            new EvaluationContext(new DictionaryVariableResolver([])));

        Assert.That(value, Is.True);
    }

    private sealed record EvaluationContext(IVariableResolver Variables)
        : IExpressionEvaluationContext;

    private sealed class DictionaryVariableResolver(
        IDictionary<string, object?> values) : IVariableResolver
    {
        private readonly IDictionary<string, object?> _values = values;

        public void Register<T>(string name, T value) =>
            _values[Normalize(name)] = value;

        public bool IsRegistered(string name) => _values.ContainsKey(Normalize(name));

        public T? Resolve<T>(string tokenValue)
        {
            return _values.TryGetValue(Normalize(tokenValue), out object? value) && value is T typed
                ? typed
                : default;
        }

        public void Clear() => _values.Clear();

        public IEnumerable<string> GetVariableNames() => _values.Keys;

        private static string Normalize(string value) =>
            value.Trim().TrimStart('[').TrimEnd(']');
    }
}
