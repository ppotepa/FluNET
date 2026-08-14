using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;
using System.Text.Json;

namespace FluNET.Execution.Commands;

public sealed record FilterJsonCommand(IExpression<JsonElement[]> Source, JsonDataExpression Predicate) : ICommand<JsonElement[]>;
public sealed record SortJsonCommand(IExpression<JsonElement[]> Source, JsonDataExpression Key) : ICommand<JsonElement[]>;
public sealed record TakeJsonCommand(IExpression<JsonElement[]> Source, IExpression<int> Count) : ICommand<JsonElement[]>;

public sealed class FilterJsonCommandBinder(LanguageSnapshot language, IValueCodecRegistry values) : ICommandBinder<FilterJsonCommand, JsonElement[]>
{
    public FilterJsonCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.data.filter.json")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        BoundArgument predicate = command[new FrameRoleId("Predicate")];
        string source = string.Join(" ", predicate.Tokens.Select(token => Unwrap(token.Text)));
        return new FilterJsonCommand(context.Require<JsonElement[]>(SemanticRole.Source), JsonDataExpression.Parse(source));
    }
    private static string Unwrap(string value) => value.Length >= 2 && value[0] == '{' && value[^1] == '}' ? value[1..^1] : value;
}

public sealed class SortJsonCommandBinder(LanguageSnapshot language, IValueCodecRegistry values) : ICommandBinder<SortJsonCommand, JsonElement[]>
{
    public SortJsonCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.data.sort.json")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        BoundArgument key = command[new FrameRoleId("Key")];
        string source = string.Join(" ", key.Tokens.Select(token => Unwrap(token.Text)));
        return new SortJsonCommand(context.Require<JsonElement[]>(SemanticRole.Source), JsonDataExpression.Parse(source));
    }
    private static string Unwrap(string value) => value.Length >= 2 && value[0] == '{' && value[^1] == '}' ? value[1..^1] : value;
}

public sealed class TakeJsonCommandBinder(LanguageSnapshot language, IValueCodecRegistry values) : ICommandBinder<TakeJsonCommand, JsonElement[]>
{
    public TakeJsonCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.data.take.json")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new TakeJsonCommand(context.Require<JsonElement[]>(SemanticRole.Source), context.Require<int>(new FrameRoleId("Count")));
    }
}

public sealed class FilterJsonCommandHandler(IVariableResolver variables) : ICommandHandler<FilterJsonCommand, JsonElement[]>
{
    public ValueTask<JsonElement[]> HandleAsync(FilterJsonCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(command.Source.Evaluate(variables).Where(item => command.Predicate.EvaluateBoolean(item, variables)).Select(item => item.Clone()).ToArray());
    }
}

public sealed class SortJsonCommandHandler(IVariableResolver variables) : ICommandHandler<SortJsonCommand, JsonElement[]>
{
    public ValueTask<JsonElement[]> HandleAsync(SortJsonCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        JsonElement[] result = command.Source.Evaluate(variables)
            .Select((item, index) => new Row(item, index, command.Key.Evaluate(item, variables)))
            .OrderBy(row => row.Key, JsonDataComparer.Instance)
            .ThenBy(row => row.Index)
            .Select(row => row.Value.Clone())
            .ToArray();
        return ValueTask.FromResult(result);
    }
    private sealed record Row(JsonElement Value, int Index, object? Key);
    private sealed class JsonDataComparer : IComparer<object?>
    {
        public static JsonDataComparer Instance { get; } = new();
        public int Compare(object? x, object? y) => JsonDataExpression.CompareValues(x, y);
    }
}

public sealed class TakeJsonCommandHandler(IVariableResolver variables) : ICommandHandler<TakeJsonCommand, JsonElement[]>
{
    public ValueTask<JsonElement[]> HandleAsync(TakeJsonCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int count = command.Count.Evaluate(variables);
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), "TAKE requires a non-negative count.");
        return ValueTask.FromResult(command.Source.Evaluate(variables).Take(count).Select(item => item.Clone()).ToArray());
    }
}
