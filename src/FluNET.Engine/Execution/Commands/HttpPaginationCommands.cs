using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;
using System.Text.Json;

namespace FluNET.Execution.Commands;

public sealed record PaginateJsonCommand(
    IExpression<Uri> Source,
    IExpression<string> ItemsPath,
    IExpression<string> NextPath,
    IExpression<int> MaxPages,
    IExpression<string>? Credential = null) : ICommand<JsonElement[]>;

public sealed class PaginateJsonCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<PaginateJsonCommand, JsonElement[]>
{
    public PaginateJsonCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("network.http.pagination")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new PaginateJsonCommand(
            context.Require<Uri>(SemanticRole.Source),
            context.RequireText(new FrameRoleId("Items")),
            context.RequireText(new FrameRoleId("Next")),
            context.Require<int>(new FrameRoleId("MaxPages")),
            HttpBinding.Credential(context));
    }
}

public sealed class PaginateJsonCommandHandler(
    IVariableResolver variables,
    IHttpJsonPaginator paginator,
    ISecretStore secrets,
    ISecretAccessPolicy secretPolicy) : ICommandHandler<PaginateJsonCommand, JsonElement[]>
{
    public async ValueTask<JsonElement[]> HandleAsync(PaginateJsonCommand command, CancellationToken cancellationToken = default)
    {
        SecretValue? credential = null;
        if (command.Credential is not null)
        {
            string name = command.Credential.Evaluate(variables);
            secretPolicy.EnsureSecretAccess(name);
            if (!secrets.TryGet(name, out credential) || credential is null)
                throw new KeyNotFoundException($"Secret '{name}' is not defined.");
        }

        IReadOnlyList<JsonElement> items = await paginator.FetchAsync(
            command.Source.Evaluate(variables),
            command.ItemsPath.Evaluate(variables),
            command.NextPath.Evaluate(variables),
            command.MaxPages.Evaluate(variables),
            credential,
            cancellationToken).ConfigureAwait(false);
        return items.ToArray();
    }
}
