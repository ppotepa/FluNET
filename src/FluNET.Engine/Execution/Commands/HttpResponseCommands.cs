using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;
using System.Text;
using System.Text.Json;

namespace FluNET.Execution.Commands;

public sealed record RequestJsonCommand(
    IExpression<Uri> Source,
    IExpression<string>? Credential = null) : ICommand<JsonElement>;

public sealed class RequestJsonCommandBinder(LanguageSnapshot language, IValueCodecRegistry values)
    : ICommandBinder<RequestJsonCommand, JsonElement>
{
    public RequestJsonCommand? TryBind(BoundCommand command)
    {
        if (command.Frame.Id != new FrameId("network.http.response")) return null;
        CommandBindingContext context = new(command, new ExpressionBinder(language, values));
        return new(context.Require<Uri>(SemanticRole.Source), context.Optional<string>(new FrameRoleId("Credential")));
    }
}

public sealed class RequestJsonCommandHandler(
    IVariableResolver variables,
    IHttpTransport http,
    IAuthenticatedHttpTransport authenticated,
    ISecretStore secrets,
    ISecretAccessPolicy secretPolicy) : ICommandHandler<RequestJsonCommand, JsonElement>
{
    public async ValueTask<JsonElement> HandleAsync(RequestJsonCommand command, CancellationToken cancellationToken = default)
    {
        Uri uri = command.Source.Evaluate(variables);
        HttpResourceResponse response;
        if (command.Credential is null)
        {
            response = await http.GetResponseAsync(uri, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            string name = command.Credential.Evaluate(variables);
            secretPolicy.EnsureSecretAccess(name);
            if (!secrets.TryGet(name, out SecretValue? secret) || secret is null)
                throw new KeyNotFoundException($"Secret '{name}' is not defined.");
            response = await authenticated.GetResponseAsync(uri, secret, cancellationToken).ConfigureAwait(false);
        }

        JsonElement body = DecodeBody(response);
        Dictionary<string, string[]> headers = response.Headers.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);
        return JsonSerializer.SerializeToElement(new
        {
            status = response.StatusCode,
            ok = response.StatusCode is >= 200 and < 300,
            headers,
            body
        });
    }

    private static JsonElement DecodeBody(HttpResourceResponse response)
    {
        string text = Encoding.UTF8.GetString(response.Content);
        if (string.IsNullOrWhiteSpace(text)) return JsonDocument.Parse("null").RootElement.Clone();
        try { return JsonDocument.Parse(text).RootElement.Clone(); }
        catch (JsonException) { return JsonSerializer.SerializeToElement(text); }
    }
}
