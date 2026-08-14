using System.Net.Http.Headers;

namespace FluNET.Capabilities;

/// <summary>Authentication policy is host-owned; the compact AUTH directive only binds a secret name.</summary>
public interface IHttpAuthenticationScheme
{
    string Id { get; }
    void Apply(HttpRequestMessage request, SecretValue credential);
}

public sealed class BearerHttpAuthenticationScheme : IHttpAuthenticationScheme
{
    public string Id => "bearer";
    public void Apply(HttpRequestMessage request, SecretValue credential)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(credential);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.Reveal());
    }
}

public interface IAuthenticatedHttpTransport
{
    Task<HttpResourceResponse> GetAsync(Uri uri, SecretValue credential, CancellationToken cancellationToken = default);
}

/// <summary>Default authenticated HTTP capability. Secret plaintext is revealed only inside this transport boundary.</summary>
public sealed class AuthenticatedHttpTransport(
    HttpClient client,
    IExecutionPolicy policy,
    IHttpAuthenticationScheme authentication) : IAuthenticatedHttpTransport
{
    public async Task<HttpResourceResponse> GetAsync(Uri uri, SecretValue credential, CancellationToken cancellationToken = default)
    {
        policy.EnsureNetworkAccess(uri);
        using HttpRequestMessage request = new(HttpMethod.Get, uri);
        authentication.Apply(request, credential);
        using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        byte[] content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<string, string[]> headers = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string,IEnumerable<string>> header in response.Headers) headers[header.Key] = header.Value.ToArray();
        foreach (KeyValuePair<string,IEnumerable<string>> header in response.Content.Headers) headers[header.Key] = header.Value.ToArray();
        response.EnsureSuccessStatusCode();
        MediaTypeHeaderValue? type = response.Content.Headers.ContentType;
        return new(content, (int)response.StatusCode, type?.MediaType, type?.CharSet, headers);
    }
}
