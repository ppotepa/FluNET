using System.Text.Json;
using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record ReceiveMessageCommand(IExpression<string> Topic) : ICommand<JsonElement>;

public sealed class ReceiveMessageCommandBinder(
    LanguageSnapshot language,
    IValueCodecRegistry values) : ICommandBinder<ReceiveMessageCommand, JsonElement>
{
    public ReceiveMessageCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("messaging.receive")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new(context.RequireText(SemanticRole.Source));
    }
}

public sealed class ReceiveMessageCommandHandler(
    IFluNetMessageBus bus,
    IVariableResolver variables) : ICommandHandler<ReceiveMessageCommand, JsonElement>
{
    public async ValueTask<JsonElement> HandleAsync(
        ReceiveMessageCommand command,
        CancellationToken cancellationToken = default)
    {
        FluNetMessage message = await bus.ReceiveAsync(
            command.Topic.Evaluate(variables), cancellationToken).ConfigureAwait(false);
        return JsonSerializer.SerializeToElement(message);
    }
}
