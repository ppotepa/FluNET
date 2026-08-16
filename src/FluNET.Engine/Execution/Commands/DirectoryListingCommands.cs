using System.Text.Json;
using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record ListDirectoryJsonCommand(IExpression<string> Path, IExpression<bool>? Recursive = null) : ICommand<JsonElement[]>;

public sealed class ListDirectoryJsonCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) : ICommandBinder<ListDirectoryJsonCommand, JsonElement[]>
{
    public ListDirectoryJsonCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.files.list.json")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new(
            context.RequireText(SemanticRole.Source),
            context.Optional<bool>(new FrameRoleId("Recursive")));
    }
}

public sealed class ListDirectoryJsonCommandHandler(
    IFluNetDirectoryOperations directories,
    IVariableResolver variables) : ICommandHandler<ListDirectoryJsonCommand, JsonElement[]>
{
    public async ValueTask<JsonElement[]> HandleAsync(
        ListDirectoryJsonCommand command,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FluNetDirectoryEntry> entries = await directories.ListAsync(
            command.Path.Evaluate(variables),
            command.Recursive?.Evaluate(variables) ?? false,
            cancellationToken).ConfigureAwait(false);
        return entries.Select(entry => JsonSerializer.SerializeToElement(new
        {
            path = entry.Path,
            name = entry.Name,
            nameWithoutExtension = Path.GetFileNameWithoutExtension(entry.Name),
            directory = Path.GetDirectoryName(entry.Path),
            extension = entry.IsDirectory ? string.Empty : Path.GetExtension(entry.Name),
            isDirectory = entry.IsDirectory,
            length = entry.Length,
            createdUtc = entry.CreatedUtc,
            modifiedUtc = entry.ModifiedUtc,
            accessedUtc = entry.IsDirectory
                ? new DirectoryInfo(entry.Path).LastAccessTimeUtc
                : new FileInfo(entry.Path).LastAccessTimeUtc,
            isHidden = entry.IsHidden,
            isReadOnly = entry.IsDirectory
                ? new DirectoryInfo(entry.Path).Attributes.HasFlag(FileAttributes.ReadOnly)
                : new FileInfo(entry.Path).IsReadOnly
        })).ToArray();
    }
}
