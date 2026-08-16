using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record CreateDirectoryCommand(IExpression<string> Path) : ICommand<DirectoryInfo>;

public sealed class CreateDirectoryCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<CreateDirectoryCommand, DirectoryInfo>
{
    public CreateDirectoryCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("filesystem.directory.create")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new(context.RequireText(SemanticRole.Source));
    }
}

public sealed class CreateDirectoryCommandHandler(
    IFluNetDirectoryOperations directories,
    IVariableResolver variables) : ICommandHandler<CreateDirectoryCommand, DirectoryInfo>
{
    public ValueTask<DirectoryInfo> HandleAsync(
        CreateDirectoryCommand command,
        CancellationToken cancellationToken = default) =>
        directories.CreateAsync(command.Path.Evaluate(variables), cancellationToken);
}
