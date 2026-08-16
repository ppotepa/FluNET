using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record TrashDirectoryCommand(IExpression<string> Source) : ICommand<DirectoryInfo>;

public sealed class TrashDirectoryCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<TrashDirectoryCommand, DirectoryInfo>
{
    public TrashDirectoryCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("filesystem.directory.trash")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new(context.RequireText(SemanticRole.Source));
    }
}

public sealed class TrashDirectoryCommandHandler(IFluNetDirectoryTrash trash, IVariableResolver variables)
    : ICommandHandler<TrashDirectoryCommand, DirectoryInfo>
{
    public ValueTask<DirectoryInfo> HandleAsync(TrashDirectoryCommand command, CancellationToken cancellationToken = default) =>
        trash.MoveDirectoryToTrashAsync(command.Source.Evaluate(variables), cancellationToken);
}
