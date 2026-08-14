using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;
using System.Globalization;
using System.Text.Json;

namespace FluNET.Execution.Commands;

public sealed record GroupJsonCommand(IExpression<JsonElement[]> Source, JsonDataExpression Key) : ICommand<JsonElement[]>;
public sealed record SumJsonCommand(IExpression<JsonElement[]> Source, JsonDataExpression Value) : ICommand<decimal>;
public sealed record JoinJsonCommand(
    IExpression<JsonElement[]> Left,
    IExpression<JsonElement[]> Right,
    JsonDataExpression LeftKey,
    JsonDataExpression RightKey) : ICommand<JsonElement[]>;

public sealed class GroupJsonCommandBinder(LanguageSnapshot language, IValueCodecRegistry values) : ICommandBinder<GroupJsonCommand, JsonElement[]>
{
    public GroupJsonCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.data.group.json")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new GroupJsonCommand(
            context.Require<JsonElement[]>(SemanticRole.Source),
            JsonDataExpression.Parse(Descriptor(command, "Key")));
    }
    private static string Descriptor(BoundCommand command, string role) =>
        string.Join(" ", command[new FrameRoleId(role)].Tokens.Select(token => Unwrap(token.Text)));
    private static string Unwrap(string value) => value.Length >= 2 && value[0] == '{' && value[^1] == '}' ? value[1..^1] : value;
}

public sealed class SumJsonCommandBinder(LanguageSnapshot language, IValueCodecRegistry values) : ICommandBinder<SumJsonCommand, decimal>
{
    public SumJsonCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.data.sum.json")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new SumJsonCommand(
            context.Require<JsonElement[]>(SemanticRole.Source),
            JsonDataExpression.Parse(Descriptor(command, "Value")));
    }
    private static string Descriptor(BoundCommand command, string role) =>
        string.Join(" ", command[new FrameRoleId(role)].Tokens.Select(token => Unwrap(token.Text)));
    private static string Unwrap(string value) => value.Length >= 2 && value[0] == '{' && value[^1] == '}' ? value[1..^1] : value;
}

public sealed class JoinJsonCommandBinder(LanguageSnapshot language, IValueCodecRegistry values) : ICommandBinder<JoinJsonCommand, JsonElement[]>
{
    public JoinJsonCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.data.join.json")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        string descriptor = string.Join("", command[new FrameRoleId("Match")].Tokens.Select(token => Unwrap(token.Text)));
        string[] parts = descriptor.Split('|', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace))
            throw new FormatException("JOIN descriptor must contain left and right key expressions.");
        return new JoinJsonCommand(
            context.Require<JsonElement[]>(SemanticRole.Source),
            context.Require<JsonElement[]>(SemanticRole.Goal),
            JsonDataExpression.Parse(parts[0]),
            JsonDataExpression.Parse(parts[1]));
    }
    private static string Unwrap(string value) => value.Length >= 2 && value[0] == '{' && value[^1] == '}' ? value[1..^1] : value;
}

public sealed class GroupJsonCommandHandler(IVariableResolver variables) : ICommandHandler<GroupJsonCommand, JsonElement[]>
{
    public ValueTask<JsonElement[]> HandleAsync(GroupJsonCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        JsonElement[] source = command.Source.Evaluate(variables);
        JsonElement[] result = source
            .Select(item => new GroupRow(item, command.Key.Evaluate(item, variables)))
            .GroupBy(row => StableKey(row.Key), StringComparer.Ordinal)
            .Select(group => JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["key"] = group.First().Key,
                ["items"] = group.Select(row => row.Item.Clone()).ToArray()
            }).Clone())
            .ToArray();
        return ValueTask.FromResult(result);
    }

    private static string StableKey(object? value) => JsonSerializer.Serialize(value);
    private sealed record GroupRow(JsonElement Item, object? Key);
}

public sealed class SumJsonCommandHandler(IVariableResolver variables) : ICommandHandler<SumJsonCommand, decimal>
{
    public ValueTask<decimal> HandleAsync(SumJsonCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        decimal total = 0m;
        foreach (JsonElement item in command.Source.Evaluate(variables))
        {
            object? value = command.Value.Evaluate(item, variables);
            if (value is null) continue;
            try { total += Convert.ToDecimal(value, CultureInfo.InvariantCulture); }
            catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
            {
                throw new FormatException($"SUM value '{value}' is not a Number.", exception);
            }
        }
        return ValueTask.FromResult(total);
    }
}

public sealed class JoinJsonCommandHandler(IVariableResolver variables) : ICommandHandler<JoinJsonCommand, JsonElement[]>
{
    public ValueTask<JsonElement[]> HandleAsync(JoinJsonCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        JsonElement[] right = command.Right.Evaluate(variables);
        Dictionary<string, List<JsonElement>> lookup = new(StringComparer.Ordinal);
        foreach (JsonElement item in right)
        {
            string key = StableKey(command.RightKey.Evaluate(item, variables));
            if (!lookup.TryGetValue(key, out List<JsonElement>? bucket)) lookup[key] = bucket = [];
            bucket.Add(item.Clone());
        }

        List<JsonElement> result = [];
        foreach (JsonElement left in command.Left.Evaluate(variables))
        {
            string key = StableKey(command.LeftKey.Evaluate(left, variables));
            if (!lookup.TryGetValue(key, out List<JsonElement>? matches)) continue;
            foreach (JsonElement rightItem in matches) result.Add(Merge(left, rightItem));
        }
        return ValueTask.FromResult(result.ToArray());
    }

    private static string StableKey(object? value) => JsonSerializer.Serialize(value);

    private static JsonElement Merge(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != JsonValueKind.Object || right.ValueKind != JsonValueKind.Object)
            return JsonSerializer.SerializeToElement(new Dictionary<string, object?> { ["left"] = left.Clone(), ["right"] = right.Clone() }).Clone();

        Dictionary<string, object?> merged = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty property in left.EnumerateObject()) merged[property.Name] = ConvertValue(property.Value);
        foreach (JsonProperty property in right.EnumerateObject())
        {
            string name = merged.ContainsKey(property.Name) ? $"right_{property.Name}" : property.Name;
            merged[name] = ConvertValue(property.Value);
        }
        return JsonSerializer.SerializeToElement(merged).Clone();
    }

    private static object? ConvertValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number when value.TryGetDecimal(out decimal number) => number,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => value.Clone()
    };
}
