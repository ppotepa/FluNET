using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record PutJsonCommand(IExpression<string> Body, IExpression<Uri> Target, IExpression<string>? Credential = null) : ICommand<string>;
public sealed record PatchJsonCommand(IExpression<string> Body, IExpression<Uri> Target, IExpression<string>? Credential = null) : ICommand<string>;
public sealed record DeleteHttpCommand(IExpression<Uri> Target, IExpression<string>? Credential = null) : ICommand<string>;

public abstract class HttpJsonMutationBinder<TCommand>(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<TCommand, string> where TCommand : class, ICommand<string>
{
    public TCommand? TryBind(BoundCommand command)
    {
        if (!Matches(command)) return default;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return Bind(context);
    }

    protected abstract bool Matches(BoundCommand command);
    protected abstract TCommand Bind(CommandBindingContext context);
}

public sealed class PutJsonCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : HttpJsonMutationBinder<PutJsonCommand>(language, values)
{
    protected override bool Matches(BoundCommand command) => command.Frame.Id == new FrameId("core.put.json");
    protected override PutJsonCommand Bind(CommandBindingContext context) =>
        new(context.RequireText(SemanticRole.Theme, preserveStructuredReferences: true), context.Require<Uri>(SemanticRole.Goal), HttpBinding.Credential(context));
}

public sealed class PatchJsonCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : HttpJsonMutationBinder<PatchJsonCommand>(language, values)
{
    protected override bool Matches(BoundCommand command) => command.Frame.Id == new FrameId("core.patch.json");
    protected override PatchJsonCommand Bind(CommandBindingContext context) =>
        new(context.RequireText(SemanticRole.Theme, preserveStructuredReferences: true), context.Require<Uri>(SemanticRole.Goal), HttpBinding.Credential(context));
}

public sealed class DeleteHttpCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<DeleteHttpCommand, string>
{
    public DeleteHttpCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("core.delete.http")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new DeleteHttpCommand(context.Require<Uri>(SemanticRole.Goal), HttpBinding.Credential(context));
    }
}

public sealed class PutJsonCommandHandler(IVariableResolver variables, IHttpTransport http, IAuthenticatedHttpTransport authenticated, ISecretStore secrets, ISecretAccessPolicy secretPolicy)
    : ICommandHandler<PutJsonCommand, string>
{
    public ValueTask<string> HandleAsync(PutJsonCommand command, CancellationToken cancellationToken = default) =>
        HttpMutationRuntime.SendAsync(HttpMethod.Put, command.Target.Evaluate(variables), command.Body.Evaluate(variables), command.Credential, variables, http, authenticated, secrets, secretPolicy, cancellationToken);
}

public sealed class PatchJsonCommandHandler(IVariableResolver variables, IHttpTransport http, IAuthenticatedHttpTransport authenticated, ISecretStore secrets, ISecretAccessPolicy secretPolicy)
    : ICommandHandler<PatchJsonCommand, string>
{
    public ValueTask<string> HandleAsync(PatchJsonCommand command, CancellationToken cancellationToken = default) =>
        HttpMutationRuntime.SendAsync(HttpMethod.Patch, command.Target.Evaluate(variables), command.Body.Evaluate(variables), command.Credential, variables, http, authenticated, secrets, secretPolicy, cancellationToken);
}

public sealed class DeleteHttpCommandHandler(IVariableResolver variables, IHttpTransport http, IAuthenticatedHttpTransport authenticated, ISecretStore secrets, ISecretAccessPolicy secretPolicy)
    : ICommandHandler<DeleteHttpCommand, string>
{
    public ValueTask<string> HandleAsync(DeleteHttpCommand command, CancellationToken cancellationToken = default) =>
        HttpMutationRuntime.SendAsync(HttpMethod.Delete, command.Target.Evaluate(variables), null, command.Credential, variables, http, authenticated, secrets, secretPolicy, cancellationToken);
}
