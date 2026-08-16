using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record TrashFileCommand(IExpression<FileInfo> Source) : ICommand<FileInfo>;

public sealed class TrashFileCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<TrashFileCommand, FileInfo>
{
    public TrashFileCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.files.trash")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new TrashFileCommand(context.Require<FileInfo>(SemanticRole.Source));
    }
}

public sealed class TrashFileCommandHandler(IFluNetFileTrash trash, IVariableResolver variables)
    : ICommandHandler<TrashFileCommand, FileInfo>
{
    public ValueTask<FileInfo> HandleAsync(TrashFileCommand command, CancellationToken cancellationToken = default) =>
        trash.MoveToTrashAsync(command.Source.Evaluate(variables).FullName, cancellationToken);
}
