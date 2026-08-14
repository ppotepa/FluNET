using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;
using System.Text.Json;

namespace FluNET.Execution.Commands;

public sealed record GetSqlCommand(IExpression<string> Query) : ICommand<JsonElement[]>;

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
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows = await sql.QueryAsync(query, cancellationToken).ConfigureAwait(false);
        return rows.Select(row => JsonSerializer.SerializeToElement(row)).ToArray();
    }
}
