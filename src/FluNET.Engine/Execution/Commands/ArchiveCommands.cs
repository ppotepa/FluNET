using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;
using System.Text.Json;

namespace FluNET.Execution.Commands;

public sealed record CreateArchiveCommand(
    IExpression<string> Source,
    IExpression<string> Destination) : ICommand<string>;

public sealed record ExtractArchiveCommand(
    IExpression<string> Source,
    IExpression<string> Destination) : ICommand<string>;

public sealed record ListArchiveCommand(
    IExpression<string> Source) : ICommand<JsonElement[]>;

public sealed class CreateArchiveCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<CreateArchiveCommand, string>
{
    public CreateArchiveCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("filesystem.archive.create")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new(context.RequireText(SemanticRole.Source), context.RequireText(SemanticRole.Goal));
    }
}

public sealed class ExtractArchiveCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<ExtractArchiveCommand, string>
{
    public ExtractArchiveCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("filesystem.archive.extract")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new(context.RequireText(SemanticRole.Source), context.RequireText(SemanticRole.Goal));
    }
}

public sealed class ListArchiveCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<ListArchiveCommand, JsonElement[]>
{
    public ListArchiveCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("filesystem.archive.list")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new(context.RequireText(SemanticRole.Source));
    }
}

public sealed class CreateArchiveCommandHandler(
    IFluNetArchive archive,
    IVariableResolver variables) : ICommandHandler<CreateArchiveCommand, string>
{
    public ValueTask<string> HandleAsync(CreateArchiveCommand command, CancellationToken cancellationToken = default) =>
        archive.CreateAsync(command.Source.Evaluate(variables), command.Destination.Evaluate(variables), cancellationToken);
}

public sealed class ExtractArchiveCommandHandler(
    IFluNetArchive archive,
    IVariableResolver variables) : ICommandHandler<ExtractArchiveCommand, string>
{
    public ValueTask<string> HandleAsync(ExtractArchiveCommand command, CancellationToken cancellationToken = default) =>
        archive.ExtractAsync(command.Source.Evaluate(variables), command.Destination.Evaluate(variables), cancellationToken);
}

public sealed class ListArchiveCommandHandler(
    IFluNetArchive archive,
    IVariableResolver variables) : ICommandHandler<ListArchiveCommand, JsonElement[]>
{
    public async ValueTask<JsonElement[]> HandleAsync(
        ListArchiveCommand command,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FluNetArchiveEntry> entries = await archive.ListAsync(
            command.Source.Evaluate(variables), cancellationToken).ConfigureAwait(false);
        return entries.Select(entry => JsonSerializer.SerializeToElement(new
        {
            path = entry.Path,
            length = entry.Length,
            compressedLength = entry.CompressedLength,
            modifiedUtc = entry.ModifiedUtc,
            isDirectory = entry.IsDirectory
        })).ToArray();
    }
}
