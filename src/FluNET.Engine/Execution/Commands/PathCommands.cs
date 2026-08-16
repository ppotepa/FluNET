using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record ResolvePathCommand(IExpression<string> Name) : ICommand<string>;

public sealed class ResolvePathCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) : ICommandBinder<ResolvePathCommand, string>
{
    public ResolvePathCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.system.path")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new(context.RequireText(SemanticRole.Source));
    }
}

public sealed class ResolvePathCommandHandler(
    IFluNetPathResolver paths,
    IVariableResolver variables) : ICommandHandler<ResolvePathCommand, string>
{
    public ValueTask<string> HandleAsync(
        ResolvePathCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(paths.Resolve(command.Name.Evaluate(variables)));
    }
}
