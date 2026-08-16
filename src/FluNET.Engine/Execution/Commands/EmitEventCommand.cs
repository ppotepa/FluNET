using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record EmitEventCommand(IExpression<string> Payload, IExpression<Uri> Target, IExpression<string>? Credential = null) : ICommand<string>;

public sealed class EmitEventCommandBinder(LanguageSnapshot language, IValueCodecRegistry values) : ICommandBinder<EmitEventCommand, string>
{
    public EmitEventCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("events.emit.webhook")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new(context.RequireText(SemanticRole.Theme, preserveStructuredReferences: true), context.Require<Uri>(SemanticRole.Goal), HttpBinding.Credential(context));
    }
}

public sealed class EmitEventCommandHandler(IVariableResolver variables, IFluNetEventSink sink, ISecretStore secrets, ISecretAccessPolicy secretPolicy) : ICommandHandler<EmitEventCommand, string>
{
    public async ValueTask<string> HandleAsync(EmitEventCommand command, CancellationToken cancellationToken = default)
    {
        SecretValue? credential = null;
        if (command.Credential is not null)
        {
            string name = command.Credential.Evaluate(variables);
            secretPolicy.EnsureSecretAccess(name);
            if (!secrets.TryGet(name, out credential) || credential is null) throw new KeyNotFoundException($"Secret '{name}' is not defined.");
        }
        return await sink.EmitAsync(command.Target.Evaluate(variables), command.Payload.Evaluate(variables), credential, cancellationToken).ConfigureAwait(false);
    }
}
