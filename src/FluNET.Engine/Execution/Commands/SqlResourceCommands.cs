using FluNET.Capabilities;
using FluNET.Compilation.Sql;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;
using System.Text.Json;

namespace FluNET.Execution.Commands;

public sealed record GetSqlCommand(IExpression<string> Query) : ICommand<JsonElement[]>;
public sealed record ApplySqlCommand(IExpression<string> Query) : ICommand<int>;

public sealed class GetSqlCommandBinder(LanguageSnapshot language, IValueCodecRegistry values) : ICommandBinder<GetSqlCommand, JsonElement[]>
{
    public GetSqlCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.get.sql")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new GetSqlCommand(context.RequireText(SemanticRole.Source));
    }
}

public sealed class GetSqlCommandHandler(
    IVariableResolver variables,
    ISqlQueryExecutor sql) : ICommandHandler<GetSqlCommand, JsonElement[]>
{
    public async ValueTask<JsonElement[]> HandleAsync(GetSqlCommand command, CancellationToken cancellationToken = default)
    {
        string query = command.Query.Evaluate(variables);
        Dictionary<string, object?> parameters = SqlParameterBinding.Resolve(query, variables);

        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows = parameters.Count == 0
            ? await sql.QueryAsync(query, cancellationToken).ConfigureAwait(false)
            : await sql.QueryAsync(query, parameters, cancellationToken).ConfigureAwait(false);
        return rows.Select(row => JsonSerializer.SerializeToElement(row)).ToArray();
    }

}

public sealed class ApplySqlCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<ApplySqlCommand, int>
{
    public ApplySqlCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.apply.sql")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new ApplySqlCommand(context.RequireText(SemanticRole.Source));
    }
}

public sealed class ApplySqlCommandHandler(
    IVariableResolver variables,
    ISqlQueryExecutor sql) : ICommandHandler<ApplySqlCommand, int>
{
    public ValueTask<int> HandleAsync(ApplySqlCommand command, CancellationToken cancellationToken = default)
    {
        string query = command.Query.Evaluate(variables);
        return sql.ExecuteAsync(query, SqlParameterBinding.Resolve(query, variables), cancellationToken);
    }
}

internal static class SqlParameterBinding
{
    public static Dictionary<string, object?> Resolve(string query, IVariableResolver variables)
    {
        Dictionary<string, object?> parameters = [];
        foreach (string name in SqlParameterScanner.Scan(query))
        {
            object? value = variables.Resolve<object>($"[{name}]");
            if (value is null && !variables.IsRegistered(name))
                throw new InvalidOperationException($"SQL parameter '${name}' has no registered FluNET variable.");
            parameters[name] = Normalize(value);
        }
        return parameters;
    }

    private static object? Normalize(object? value) => value is JsonElement json
        ? json.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => json.GetString(),
            JsonValueKind.Number when json.TryGetInt64(out long integer) => integer,
            JsonValueKind.Number when json.TryGetDecimal(out decimal number) => number,
            JsonValueKind.True or JsonValueKind.False => json.GetBoolean(),
            _ => json.GetRawText()
        }
        : value;
}
