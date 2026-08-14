using FluNET.Execution.Commands;
using FluNET.Language;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Tests.Execution;

[TestFixture]
public sealed class InterpolatedTextExpressionTests
{
    [Test]
    public void InterpolationReadsPropertiesAndUsesLanguageConversions()
    {
        LanguageSnapshot language = StandardLanguage.CreateSnapshot();
        IValueCodecRegistry values = ValueCodecRegistryFactory.CreateDefault(language);
        bool created = InterpolatedTextExpression.TryCreate(
            "{post.title} — {todo.title} #{post.id}",
            language,
            values,
            out IExpression<string>? expression);
        DictionaryResolver resolver = new(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["post"] = new Dictionary<string, object?> { ["title"] = "Hello", ["id"] = 42m },
            ["todo"] = new Dictionary<string, object?> { ["title"] = "World" }
        });

        string result = expression!.Evaluate(new ExpressionEvaluationContext(resolver));

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.True);
            Assert.That(result, Is.EqualTo("Hello — World #42"));
            Assert.That(((InterpolatedTextExpression)expression).VariableReferences,
                Is.EquivalentTo(new[] { "post", "todo" }));
        });
    }

    [Test]
    public void DynamicPathReadsListIndexes()
    {
        Assert.That(DynamicPathExpression.TryParse("posts[1].title", out DynamicPathExpression? path), Is.True);
        DictionaryResolver resolver = new(new Dictionary<string, object?>
        {
            ["posts"] = new object?[]
            {
                new Dictionary<string, object?> { ["title"] = "First" },
                new Dictionary<string, object?> { ["title"] = "Second" }
            }
        });

        Assert.That(path!.Evaluate(new ExpressionEvaluationContext(resolver)), Is.EqualTo("Second"));
    }

    private sealed class DictionaryResolver(IDictionary<string, object?> values) : IVariableResolver
    {
        public void Register<T>(string name, T value) => values[Normalize(name)] = value;
        public bool IsRegistered(string name) => values.ContainsKey(Normalize(name));
        public T? Resolve<T>(string tokenValue) =>
            values.TryGetValue(Normalize(tokenValue), out object? value) && value is T typed ? typed : default;
        public void Clear() => values.Clear();
        public IEnumerable<string> GetVariableNames() => values.Keys;
        private static string Normalize(string value) => value.Trim().TrimStart('[').TrimEnd(']');
    }
}
