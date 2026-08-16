using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record CopyFileCommand(IExpression<FileInfo> Source, IExpression<FileInfo> Goal) : ICommand<FileInfo>;
public sealed record MoveFileCommand(IExpression<FileInfo> Source, IExpression<FileInfo> Goal) : ICommand<FileInfo>;

public sealed class CopyFileCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<CopyFileCommand, FileInfo>
{
    public CopyFileCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.files.copy")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new CopyFileCommand(context.Require<FileInfo>(SemanticRole.Source), context.Require<FileInfo>(SemanticRole.Goal));
    }
}

public sealed class MoveFileCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<MoveFileCommand, FileInfo>
{
    public MoveFileCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.files.move")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new MoveFileCommand(context.Require<FileInfo>(SemanticRole.Source), context.Require<FileInfo>(SemanticRole.Goal));
    }
}

public sealed class CopyFileCommandHandler(IFluNetFileOperations operations, IVariableResolver variables)
    : ICommandHandler<CopyFileCommand, FileInfo>
{
    public ValueTask<FileInfo> HandleAsync(CopyFileCommand command, CancellationToken cancellationToken = default) =>
        operations.CopyAsync(command.Source.Evaluate(variables).FullName, command.Goal.Evaluate(variables).FullName, cancellationToken);
}

public sealed class MoveFileCommandHandler(IFluNetFileOperations operations, IVariableResolver variables)
    : ICommandHandler<MoveFileCommand, FileInfo>
{
    public ValueTask<FileInfo> HandleAsync(MoveFileCommand command, CancellationToken cancellationToken = default) =>
        operations.MoveAsync(command.Source.Evaluate(variables).FullName, command.Goal.Evaluate(variables).FullName, cancellationToken);
}
