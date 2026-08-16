using FluNET.Capabilities;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

internal static class HttpMutationRuntime
{
    public static async ValueTask<string> SendAsync(
        HttpMethod method,
        Uri uri,
        string? body,
        IExpression<string>? credential,
        IVariableResolver variables,
        IHttpTransport http,
        IAuthenticatedHttpTransport authenticated,
        ISecretStore secrets,
        ISecretAccessPolicy secretPolicy,
        CancellationToken cancellationToken)
    {
        if (credential is null)
            return await SendUnauthenticatedAsync(method, uri, body, http, cancellationToken).ConfigureAwait(false);

        string name = credential.Evaluate(variables);
        secretPolicy.EnsureSecretAccess(name);
        if (!secrets.TryGet(name, out SecretValue? secret) || secret is null)
            throw new KeyNotFoundException($"Secret '{name}' is not defined.");

        Task<string> authenticatedRequest = method.Method.ToUpperInvariant() switch
        {
            "POST" => authenticated.PostJsonAsync(uri, body ?? string.Empty, secret, cancellationToken),
            "PUT" => authenticated.PutJsonAsync(uri, body ?? string.Empty, secret, cancellationToken),
            "PATCH" => authenticated.PatchJsonAsync(uri, body ?? string.Empty, secret, cancellationToken),
            "DELETE" => authenticated.DeleteAsync(uri, secret, cancellationToken),
            _ => throw new NotSupportedException($"HTTP method '{method}' is not supported.")
        };
        return await authenticatedRequest.ConfigureAwait(false);
    }

    private static Task<string> SendUnauthenticatedAsync(
        HttpMethod method,
        Uri uri,
        string? body,
        IHttpTransport http,
        CancellationToken cancellationToken) => method.Method.ToUpperInvariant() switch
        {
            "POST" => http.PostJsonAsync(uri, body ?? string.Empty, cancellationToken),
            "PUT" => http.PutJsonAsync(uri, body ?? string.Empty, cancellationToken),
            "PATCH" => http.PatchJsonAsync(uri, body ?? string.Empty, cancellationToken),
            "DELETE" => http.DeleteAsync(uri, cancellationToken),
            _ => throw new NotSupportedException($"HTTP method '{method}' is not supported.")
        };
}
