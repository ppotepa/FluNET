using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record RestoreFileCommand(IExpression<FileInfo> Source, IExpression<FileInfo> Goal) : ICommand<FileInfo>;
public sealed record RestoreDirectoryCommand(IExpression<DirectoryInfo> Source, IExpression<DirectoryInfo> Goal) : ICommand<DirectoryInfo>;

public sealed class RestoreFileCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<RestoreFileCommand, FileInfo>
{
    public RestoreFileCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("filesystem.trash.restore.file")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new(context.Require<FileInfo>(SemanticRole.Source), context.Require<FileInfo>(SemanticRole.Goal));
    }
}

public sealed class RestoreDirectoryCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<RestoreDirectoryCommand, DirectoryInfo>
{
    public RestoreDirectoryCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("filesystem.trash.restore.directory")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new(context.Require<DirectoryInfo>(SemanticRole.Source), context.Require<DirectoryInfo>(SemanticRole.Goal));
    }
}

public sealed class RestoreFileCommandHandler(IFluNetFileRestore restore, IVariableResolver variables)
    : ICommandHandler<RestoreFileCommand, FileInfo>
{
    public ValueTask<FileInfo> HandleAsync(RestoreFileCommand command, CancellationToken cancellationToken = default) =>
        restore.RestoreFileAsync(command.Source.Evaluate(variables).FullName, command.Goal.Evaluate(variables).FullName, cancellationToken);
}

public sealed class RestoreDirectoryCommandHandler(IFluNetDirectoryRestore restore, IVariableResolver variables)
    : ICommandHandler<RestoreDirectoryCommand, DirectoryInfo>
{
    public ValueTask<DirectoryInfo> HandleAsync(RestoreDirectoryCommand command, CancellationToken cancellationToken = default) =>
        restore.RestoreDirectoryAsync(command.Source.Evaluate(variables).FullName, command.Goal.Evaluate(variables).FullName, cancellationToken);
}
