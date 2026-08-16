using System.Text.Json;
using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record SearchFilesCommand(
    IExpression<string> Root,
    IExpression<string> Query,
    IExpression<bool>? Recursive = null,
    IExpression<bool>? Regex = null,
    IExpression<int>? Limit = null) : ICommand<JsonElement[]>;

public sealed class SearchFilesCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<SearchFilesCommand, JsonElement[]>
{
    public SearchFilesCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("filesystem.search")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new(
            context.RequireText(SemanticRole.Source),
            context.RequireText(new FrameRoleId("Query")),
            context.Optional<bool>(new FrameRoleId("Recursive")),
            context.Optional<bool>(new FrameRoleId("Regex")),
            context.Optional<int>(new FrameRoleId("Limit")));
    }
}

public sealed class SearchFilesCommandHandler(
    IFluNetFileSearcher searcher,
    IVariableResolver variables) : ICommandHandler<SearchFilesCommand, JsonElement[]>
{
    public async ValueTask<JsonElement[]> HandleAsync(SearchFilesCommand command, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FluNetFileSearchMatch> matches = await searcher.SearchAsync(
            command.Root.Evaluate(variables),
            command.Query.Evaluate(variables),
            command.Recursive?.Evaluate(variables) ?? false,
            command.Regex?.Evaluate(variables) ?? false,
            command.Limit?.Evaluate(variables) ?? 0,
            cancellationToken).ConfigureAwait(false);
        return matches.Select(match => JsonSerializer.SerializeToElement(new
        {
            path = match.Path,
            line = match.Line,
            column = match.Column,
            text = match.Text
        })).ToArray();
    }
}
