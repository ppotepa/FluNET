using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record NotifyCommand(IExpression<string> Message) : ICommand<string>;

public sealed class NotifyCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) : ICommandBinder<NotifyCommand, string>
{
    public NotifyCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("surface.system.notify")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new(context.RequireText(SemanticRole.Theme));
    }
}

public sealed class NotifyCommandHandler(
    IFluNetNotifier notifier,
    IVariableResolver variables) : ICommandHandler<NotifyCommand, string>
{
    public async ValueTask<string> HandleAsync(
        NotifyCommand command,
        CancellationToken cancellationToken = default)
    {
        string message = command.Message.Evaluate(variables);
        await notifier.NotifyAsync(message, cancellationToken).ConfigureAwait(false);
        return message;
    }
}
