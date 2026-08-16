using System.Net.Http.Headers;

using System.Text;

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
    async Task<HttpResourceResponse> GetResponseAsync(Uri uri, SecretValue credential, CancellationToken cancellationToken = default) =>
        await GetAsync(uri, credential, cancellationToken).ConfigureAwait(false);
    Task<HttpResourceResponse> GetAsync(Uri uri, SecretValue credential, CancellationToken cancellationToken = default);
    Task<string> PostJsonAsync(Uri uri, string json, SecretValue credential, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This authenticated HTTP provider does not support JSON POST.");
    Task<string> PutJsonAsync(Uri uri, string json, SecretValue credential, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This authenticated HTTP provider does not support JSON PUT.");
    Task<string> PatchJsonAsync(Uri uri, string json, SecretValue credential, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This authenticated HTTP provider does not support JSON PATCH.");
    Task<string> DeleteAsync(Uri uri, SecretValue credential, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This authenticated HTTP provider does not support HTTP DELETE.");
}

public sealed class ApiKeyHttpAuthenticationScheme(string headerName = "X-API-Key") : IHttpAuthenticationScheme
{
    public string Id => "api-key";
    public string HeaderName { get; } = ValidateHeaderName(headerName);

    public void Apply(HttpRequestMessage request, SecretValue credential)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(credential);
        request.Headers.Remove(HeaderName);
        request.Headers.TryAddWithoutValidation(HeaderName, credential.Reveal());
    }

    private static string ValidateHeaderName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!value.All(ch => char.IsLetterOrDigit(ch) || ch == '-'))
            throw new ArgumentException("API key header name contains invalid characters.", nameof(value));
        return value;
    }
}

public sealed class BasicHttpAuthenticationScheme : IHttpAuthenticationScheme
{
    public string Id => "basic";

    public void Apply(HttpRequestMessage request, SecretValue credential)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(credential);
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(credential.Reveal()));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);
    }
}

/// <summary>Default authenticated HTTP capability. Secret plaintext is revealed only inside this transport boundary.</summary>
public sealed class AuthenticatedHttpTransport(
    HttpClient client,
    IExecutionPolicy policy,
    IHttpAuthenticationScheme authentication) : IAuthenticatedHttpTransport
{
    public async Task<HttpResourceResponse> GetResponseAsync(Uri uri, SecretValue credential, CancellationToken cancellationToken = default)
    {
        policy.EnsureNetworkAccess(uri);
        using HttpRequestMessage request = new(HttpMethod.Get, uri);
        authentication.Apply(request, credential);
        using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        byte[] content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<string, string[]> headers = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string,IEnumerable<string>> header in response.Headers) headers[header.Key] = header.Value.ToArray();
        foreach (KeyValuePair<string,IEnumerable<string>> header in response.Content.Headers) headers[header.Key] = header.Value.ToArray();
        MediaTypeHeaderValue? type = response.Content.Headers.ContentType;
        return new(content, (int)response.StatusCode, type?.MediaType, type?.CharSet, headers);
    }

    public async Task<HttpResourceResponse> GetAsync(Uri uri, SecretValue credential, CancellationToken cancellationToken = default)
    {
        HttpResourceResponse response = await GetResponseAsync(uri, credential, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode < 200 || response.StatusCode >= 300)
            throw new HttpRequestException($"HTTP request to '{uri}' returned status {response.StatusCode}.", null, (System.Net.HttpStatusCode)response.StatusCode);
        return response;
    }

    public Task<string> PostJsonAsync(Uri uri, string json, SecretValue credential, CancellationToken cancellationToken = default) =>
        SendMutationAsync(HttpMethod.Post, uri, json, credential, cancellationToken);

    public Task<string> PutJsonAsync(Uri uri, string json, SecretValue credential, CancellationToken cancellationToken = default) =>
        SendMutationAsync(HttpMethod.Put, uri, json, credential, cancellationToken);

    public Task<string> PatchJsonAsync(Uri uri, string json, SecretValue credential, CancellationToken cancellationToken = default) =>
        SendMutationAsync(new HttpMethod("PATCH"), uri, json, credential, cancellationToken);

    public Task<string> DeleteAsync(Uri uri, SecretValue credential, CancellationToken cancellationToken = default) =>
        SendMutationAsync(HttpMethod.Delete, uri, null, credential, cancellationToken);

    private async Task<string> SendMutationAsync(
        HttpMethod method,
        Uri uri,
        string? json,
        SecretValue credential,
        CancellationToken cancellationToken)
    {
        policy.EnsureNetworkAccess(uri);
        using HttpRequestMessage request = new(method, uri);
        authentication.Apply(request, credential);
        if (json is not null)
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }
}
