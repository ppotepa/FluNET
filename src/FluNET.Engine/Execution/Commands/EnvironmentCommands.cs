using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record SetEnvironmentCommand(IExpression<string> Name, IExpression<string> Value) : ICommand<string>;

public sealed class SetEnvironmentCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<SetEnvironmentCommand, string>
{
    public SetEnvironmentCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.system.environment.write")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new(context.RequireText(SemanticRole.Source), context.RequireText(SemanticRole.Goal));
    }
}

public sealed class SetEnvironmentCommandHandler(IEnvironmentWriter environment, IVariableResolver variables)
    : ICommandHandler<SetEnvironmentCommand, string>
{
    public ValueTask<string> HandleAsync(SetEnvironmentCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string name = command.Name.Evaluate(variables);
        string value = command.Value.Evaluate(variables);
        environment.Set(name, value);
        return ValueTask.FromResult(value);
    }
}
