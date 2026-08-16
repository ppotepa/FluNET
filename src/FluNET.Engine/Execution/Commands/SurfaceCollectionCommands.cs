using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Capabilities;
using FluNET.Variables;
using System.Text.Json;

namespace FluNET.Execution.Commands;

public sealed record DistinctJsonCommand(
    IExpression<JsonElement[]> Source,
    JsonDataExpression? Key) : ICommand<JsonElement[]>;

public sealed record SkipJsonCommand(
    IExpression<JsonElement[]> Source,
    IExpression<int> Count) : ICommand<JsonElement[]>;

public sealed record SaveJsonCommand(
    IExpression<JsonElement[]> Theme,
    IExpression<FileInfo> Goal) : ICommand<string>;

public sealed record SaveCsvCommand(
    IExpression<JsonElement[]> Theme,
    IExpression<FileInfo> Goal) : ICommand<string>;

public enum JsonAggregateKind { Count, Average, Minimum, Maximum }

public sealed record AggregateJsonCommand(
    IExpression<JsonElement[]> Source,
    JsonDataExpression? Value,
    JsonAggregateKind Kind) : ICommand<decimal>;

public sealed class DistinctJsonCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<DistinctJsonCommand, JsonElement[]>
{
    public DistinctJsonCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.data.distinct.json")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        JsonDataExpression? key = null;
        if (command.Arguments.TryGetValue(new FrameRoleId("Key"), out BoundArgument? argument))
        {
            string source = string.Join(" ", argument.Tokens.Select(token => Unwrap(token.Text))).Trim();
            if (source.Length > 0) key = JsonDataExpression.Parse(source);
        }
        return new DistinctJsonCommand(context.Require<JsonElement[]>(SemanticRole.Source), key);
    }

    private static string Unwrap(string value) =>
        value.Length >= 2 && value[0] == '{' && value[^1] == '}' ? value[1..^1] : value;
}

public sealed class SkipJsonCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<SkipJsonCommand, JsonElement[]>
{
    public SkipJsonCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.data.skip.json")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new SkipJsonCommand(
            context.Require<JsonElement[]>(SemanticRole.Source),
            context.Require<int>(new FrameRoleId("Count")));
    }
}

public sealed class SaveJsonCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<SaveJsonCommand, string>
{
    public SaveJsonCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("core.save.json")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new SaveJsonCommand(
            context.Require<JsonElement[]>(SemanticRole.Theme),
            context.Require<FileInfo>(SemanticRole.Goal));
    }
}

public sealed class SaveCsvCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<SaveCsvCommand, string>
{
    public SaveCsvCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("core.save.csv")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new SaveCsvCommand(
            context.Require<JsonElement[]>(SemanticRole.Theme),
            context.Require<FileInfo>(SemanticRole.Goal));
    }
}

public sealed class AggregateJsonCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<AggregateJsonCommand, decimal>
{
    public AggregateJsonCommand? TryBind(BoundCommand command)
    {
        string frame = command.Frame.Id.Value;
        JsonAggregateKind kind = frame switch
        {
            "surface.data.count.json" => JsonAggregateKind.Count,
            "surface.data.avg.json" => JsonAggregateKind.Average,
            "surface.data.min.json" => JsonAggregateKind.Minimum,
            "surface.data.max.json" => JsonAggregateKind.Maximum,
            _ => throw new InvalidOperationException($"Unknown aggregate frame '{frame}'.")
        };
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        JsonDataExpression? value = null;
        if (command.Arguments.TryGetValue(new FrameRoleId("Value"), out BoundArgument? argument))
        {
            string source = string.Join(" ", argument.Tokens.Select(token => Unwrap(token.Text))).Trim();
            if (source.Length > 0) value = JsonDataExpression.Parse(source);
        }
        return new AggregateJsonCommand(context.Require<JsonElement[]>(SemanticRole.Source), value, kind);
    }

    private static string Unwrap(string value) =>
        value.Length >= 2 && value[0] == '{' && value[^1] == '}' ? value[1..^1] : value;
}

public sealed class DistinctJsonCommandHandler(IVariableResolver variables)
    : ICommandHandler<DistinctJsonCommand, JsonElement[]>
{
    public ValueTask<JsonElement[]> HandleAsync(
        DistinctJsonCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HashSet<string> seen = new(StringComparer.Ordinal);
        List<JsonElement> result = [];
        foreach (JsonElement item in command.Source.Evaluate(variables))
        {
            object? value = command.Key?.Evaluate(item, variables) ?? item;
            string key = JsonSerializer.Serialize(value);
            if (seen.Add(key)) result.Add(item.Clone());
        }
        return ValueTask.FromResult(result.ToArray());
    }
}

public sealed class SkipJsonCommandHandler(IVariableResolver variables)
    : ICommandHandler<SkipJsonCommand, JsonElement[]>
{
    public ValueTask<JsonElement[]> HandleAsync(
        SkipJsonCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int count = command.Count.Evaluate(variables);
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), "SKIP requires a non-negative count.");
        return ValueTask.FromResult(command.Source.Evaluate(variables).Skip(count).Select(item => item.Clone()).ToArray());
    }
}

public sealed class SaveJsonCommandHandler(IVariableResolver variables, IFluNetFileSystem files)
    : ICommandHandler<SaveJsonCommand, string>
{
    public async ValueTask<string> HandleAsync(
        SaveJsonCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        JsonElement[] value = command.Theme.Evaluate(variables);
        FileInfo goal = command.Goal.Evaluate(variables);
        await files.WriteAllBytesAsync(goal.FullName, JsonSerializer.SerializeToUtf8Bytes(value), cancellationToken).ConfigureAwait(false);
        return goal.FullName;
    }
}

public sealed class SaveCsvCommandHandler(IVariableResolver variables, IFluNetFileSystem files)
    : ICommandHandler<SaveCsvCommand, string>
{
    public async ValueTask<string> HandleAsync(
        SaveCsvCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        JsonElement[] rows = command.Theme.Evaluate(variables);
        FileInfo goal = command.Goal.Evaluate(variables);
        List<string> headers = [];
        foreach (JsonElement row in rows)
        {
            if (row.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("SAVE CSV requires an array of JSON objects.");
            foreach (JsonProperty property in row.EnumerateObject())
                if (!headers.Contains(property.Name, StringComparer.OrdinalIgnoreCase)) headers.Add(property.Name);
        }

        List<string> lines = [string.Join(',', headers.Select(Escape))];
        foreach (JsonElement row in rows)
        {
            lines.Add(string.Join(',', headers.Select(header =>
            {
                if (!row.TryGetProperty(header, out JsonElement value)) return string.Empty;
                return Escape(CsvText(value));
            })));
        }

        await files.WriteAllTextAsync(goal.FullName, string.Join("\r\n", lines) + "\r\n", cancellationToken)
            .ConfigureAwait(false);
        return goal.FullName;
    }

    private static string CsvText(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Number => value.GetRawText(),
        _ => value.GetRawText()
    };

    private static string Escape(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
}

public sealed class AggregateJsonCommandHandler(IVariableResolver variables)
    : ICommandHandler<AggregateJsonCommand, decimal>
{
    public ValueTask<decimal> HandleAsync(
        AggregateJsonCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        JsonElement[] rows = command.Source.Evaluate(variables);
        if (command.Kind == JsonAggregateKind.Count)
            return ValueTask.FromResult((decimal)rows.Length);

        decimal[] values = rows
            .Select(row => command.Value?.Evaluate(row, variables))
            .Where(value => value is not null)
            .Select(ToDecimal)
            .ToArray();
        if (values.Length == 0) throw new InvalidOperationException("Aggregate requires at least one numeric value.");
        decimal result = command.Kind switch
        {
            JsonAggregateKind.Average => values.Average(),
            JsonAggregateKind.Minimum => values.Min(),
            JsonAggregateKind.Maximum => values.Max(),
            _ => throw new InvalidOperationException()
        };
        return ValueTask.FromResult(result);
    }

    private static decimal ToDecimal(object? value) => value switch
    {
        decimal number => number,
        JsonElement json when json.ValueKind == JsonValueKind.Number => json.GetDecimal(),
        _ when value is not null && decimal.TryParse(value.ToString(), System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out decimal number) => number,
        _ => throw new InvalidOperationException($"Aggregate value '{value}' is not numeric.")
    };
}
