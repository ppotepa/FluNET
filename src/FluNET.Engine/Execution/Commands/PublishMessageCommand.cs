using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record PublishMessageCommand(
    IExpression<string> Payload,
    IExpression<string> Topic) : ICommand<string>;

public sealed class PublishMessageCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) : ICommandBinder<PublishMessageCommand, string>
{
    public PublishMessageCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("messaging.publish")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new(
            context.RequireText(SemanticRole.Theme),
            context.RequireText(SemanticRole.Goal));
    }
}

public sealed class PublishMessageCommandHandler(
    IFluNetMessageBus bus,
    IVariableResolver variables) : ICommandHandler<PublishMessageCommand, string>
{
    public async ValueTask<string> HandleAsync(
        PublishMessageCommand command,
        CancellationToken cancellationToken = default)
    {
        string payload = command.Payload.Evaluate(variables);
        string topic = command.Topic.Evaluate(variables);
        await bus.PublishAsync(topic, payload, cancellationToken).ConfigureAwait(false);
        return payload;
    }
}
