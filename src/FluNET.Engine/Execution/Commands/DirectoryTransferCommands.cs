using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record CopyDirectoryCommand(IExpression<string> Source, IExpression<string> Goal) : ICommand<DirectoryInfo>;
public sealed record MoveDirectoryCommand(IExpression<string> Source, IExpression<string> Goal) : ICommand<DirectoryInfo>;

public sealed class CopyDirectoryCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<CopyDirectoryCommand, DirectoryInfo>
{
    public CopyDirectoryCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("filesystem.directory.copy")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new(context.RequireText(SemanticRole.Source), context.RequireText(SemanticRole.Goal));
    }
}

public sealed class MoveDirectoryCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<MoveDirectoryCommand, DirectoryInfo>
{
    public MoveDirectoryCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("filesystem.directory.move")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new(context.RequireText(SemanticRole.Source), context.RequireText(SemanticRole.Goal));
    }
}

public sealed class CopyDirectoryCommandHandler(IFluNetFileOperations operations, IVariableResolver variables)
    : ICommandHandler<CopyDirectoryCommand, DirectoryInfo>
{
    public ValueTask<DirectoryInfo> HandleAsync(CopyDirectoryCommand command, CancellationToken cancellationToken = default) =>
        operations.CopyDirectoryAsync(command.Source.Evaluate(variables), command.Goal.Evaluate(variables), cancellationToken);
}

public sealed class MoveDirectoryCommandHandler(IFluNetFileOperations operations, IVariableResolver variables)
    : ICommandHandler<MoveDirectoryCommand, DirectoryInfo>
{
    public ValueTask<DirectoryInfo> HandleAsync(MoveDirectoryCommand command, CancellationToken cancellationToken = default) =>
        operations.MoveDirectoryAsync(command.Source.Evaluate(variables), command.Goal.Evaluate(variables), cancellationToken);
}
