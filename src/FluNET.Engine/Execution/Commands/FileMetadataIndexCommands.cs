using System.Text.Json;
using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record IndexFilesCommand(IExpression<string> Root, IExpression<bool>? Recursive = null) : ICommand<JsonElement[]>;

public sealed class IndexFilesCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) : ICommandBinder<IndexFilesCommand, JsonElement[]>
{
    public IndexFilesCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.files.index")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new(context.RequireText(SemanticRole.Source), context.Optional<bool>(new FrameRoleId("Recursive")));
    }
}

public sealed class IndexFilesCommandHandler(
    IFluNetFileMetadataIndex index,
    IVariableResolver variables) : ICommandHandler<IndexFilesCommand, JsonElement[]>
{
    public async ValueTask<JsonElement[]> HandleAsync(IndexFilesCommand command, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FluNetFileIndexEntry> entries = await index.RebuildAsync(
            command.Root.Evaluate(variables), command.Recursive?.Evaluate(variables) ?? true, cancellationToken).ConfigureAwait(false);
        return entries.Select(entry => JsonSerializer.SerializeToElement(new
        {
            path = entry.Path, name = entry.Name, extension = entry.Extension, length = entry.Length,
            modifiedUtc = entry.ModifiedUtc, createdUtc = entry.CreatedUtc,
            isHidden = entry.IsHidden, isReadOnly = entry.IsReadOnly
        })).ToArray();
    }
}

public sealed record ReadIndexFilesCommand(
    IExpression<string> Root,
    IExpression<string>? Predicate = null,
    IExpression<string>? OrderBy = null,
    IExpression<int>? Take = null,
    IExpression<int>? Skip = null) : ICommand<JsonElement[]>;

public sealed class ReadIndexFilesCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) : ICommandBinder<ReadIndexFilesCommand, JsonElement[]>
{
    public ReadIndexFilesCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.files.index.read")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new(
            context.RequireText(SemanticRole.Source),
            context.Optional<string>(new FrameRoleId("Predicate")),
            context.Optional<string>(new FrameRoleId("OrderBy")),
            context.Optional<int>(new FrameRoleId("Take")),
            context.Optional<int>(new FrameRoleId("Skip")));
    }
}

public sealed class ReadIndexFilesCommandHandler(
    IFluNetFileMetadataIndex index,
    IVariableResolver variables) : ICommandHandler<ReadIndexFilesCommand, JsonElement[]>
{
    public async ValueTask<JsonElement[]> HandleAsync(ReadIndexFilesCommand command, CancellationToken cancellationToken = default)
    {
        FluNetFileIndexQuery query = new(
            command.Predicate?.Evaluate(variables),
            command.OrderBy?.Evaluate(variables),
            command.Skip?.Evaluate(variables) ?? 0,
            command.Take?.Evaluate(variables));
        IReadOnlyList<FluNetFileIndexEntry> entries = await index.QueryAsync(command.Root.Evaluate(variables), query, cancellationToken).ConfigureAwait(false);
        return entries.Select(entry => JsonSerializer.SerializeToElement(new
        {
            path = entry.Path, name = entry.Name, extension = entry.Extension, length = entry.Length,
            modifiedUtc = entry.ModifiedUtc, createdUtc = entry.CreatedUtc,
            isHidden = entry.IsHidden, isReadOnly = entry.IsReadOnly
        })).ToArray();
    }
}
