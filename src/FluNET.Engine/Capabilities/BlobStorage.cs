using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace FluNET.Capabilities;

public interface IFluNetBlobStore
{
    ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default);
    ValueTask PutAsync(string key, string value, CancellationToken cancellationToken = default);
    ValueTask<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<string>> ListAsync(string prefix = "", CancellationToken cancellationToken = default);
}

public sealed class InMemoryFluNetBlobStore : IFluNetBlobStore
{
    private readonly ConcurrentDictionary<string, string> values = new(StringComparer.Ordinal);

    public ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        cancellationToken.ThrowIfCancellationRequested();
        values.TryGetValue(key, out string? value);
        return ValueTask.FromResult(value);
    }

    public ValueTask PutAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();
        values[key] = value;
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(values.TryRemove(key, out _));
    }

    public ValueTask<IReadOnlyList<string>> ListAsync(string prefix = "", CancellationToken cancellationToken = default)
    {
        ValidatePrefix(prefix);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IReadOnlyList<string>>(values.Keys
            .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray());
    }

    internal static void ValidateKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (key.StartsWith('/') || key.StartsWith('\\') ||
            key.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("A blob key must be relative and cannot contain '..'.", nameof(key));
    }

    internal static void ValidatePrefix(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        if (prefix.StartsWith('/') || prefix.StartsWith('\\') || prefix.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("A blob prefix must be relative and cannot contain '..'.", nameof(prefix));
    }
}

public sealed class FileFluNetBlobStore : IFluNetBlobStore
{
    private readonly string root;
    private readonly IExecutionPolicy policy;

    public FileFluNetBlobStore(string root, IExecutionPolicy policy)
    {
        this.root = Path.GetFullPath(root ?? throw new ArgumentNullException(nameof(root)))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public async ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        string path = Resolve(key);
        if (!File.Exists(path)) return null;
        policy.EnsureFileAccess(path);
        return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask PutAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        string path = Resolve(key);
        policy.EnsureFileAccess(path);
        string? directory = Path.GetDirectoryName(path);
        if (directory is not null) Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(path, value, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        string path = Resolve(key);
        cancellationToken.ThrowIfCancellationRequested();
        policy.EnsureFileAccess(path);
        bool existed = File.Exists(path);
        if (existed) File.Delete(path);
        return ValueTask.FromResult(existed);
    }

    public ValueTask<IReadOnlyList<string>> ListAsync(string prefix = "", CancellationToken cancellationToken = default)
    {
        InMemoryFluNetBlobStore.ValidatePrefix(prefix);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(root)) return ValueTask.FromResult<IReadOnlyList<string>>([]);
        string normalized = prefix.Replace('\\', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
        string rootPrefix = root + Path.DirectorySeparatorChar;
        IReadOnlyList<string> keys = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => { policy.EnsureFileAccess(path); return path[rootPrefix.Length..].Replace(Path.DirectorySeparatorChar, '/'); })
            .Where(key => key.StartsWith(normalized, StringComparison.Ordinal))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        return ValueTask.FromResult(keys);
    }

    private string Resolve(string key)
    {
        InMemoryFluNetBlobStore.ValidateKey(key);
        string path = Path.GetFullPath(Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = root + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal))
            throw new CapabilityDeniedException($"Blob key escapes the configured root: {key}");
        return path;
    }
}

/// <summary>
/// Provider-neutral HTTP object storage seam. It knows only a base URI and the
/// blob contract, so hosts can point it at an object gateway, signed proxy or
/// custom storage API without changing the FluNET language.
/// </summary>
public sealed class HttpFluNetBlobStore : IFluNetBlobStore
{
    private readonly Uri baseUri;
    private readonly HttpClient client;
    private readonly IExecutionPolicy policy;
    private readonly SecretValue? credential;
    private readonly IHttpAuthenticationScheme authentication;

    public HttpFluNetBlobStore(
        Uri baseUri,
        HttpClient client,
        IExecutionPolicy policy,
        SecretValue? credential = null,
        IHttpAuthenticationScheme? authentication = null)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        if (!baseUri.IsAbsoluteUri || baseUri.Scheme is not ("http" or "https"))
            throw new ArgumentException("Blob base URI must be an absolute HTTP(S) URI.", nameof(baseUri));
        this.baseUri = baseUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? baseUri
            : new Uri(baseUri.AbsoluteUri + "/", UriKind.Absolute);
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
        this.credential = credential;
        this.authentication = authentication ?? new BearerHttpAuthenticationScheme();
    }

    public async ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        Uri uri = Resolve(key);
        policy.EnsureNetworkAccess(uri);
        using HttpRequestMessage request = new(HttpMethod.Get, uri);
        ApplyAuthentication(request);
        using HttpResponseMessage response = await client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask PutAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        Uri uri = Resolve(key);
        policy.EnsureNetworkAccess(uri);
        using HttpRequestMessage request = new(HttpMethod.Put, uri)
        {
            Content = new StringContent(value, Encoding.UTF8, "text/plain")
        };
        ApplyAuthentication(request);
        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async ValueTask<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        Uri uri = Resolve(key);
        policy.EnsureNetworkAccess(uri);
        using HttpRequestMessage request = new(HttpMethod.Delete, uri);
        ApplyAuthentication(request);
        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }

    public async ValueTask<IReadOnlyList<string>> ListAsync(string prefix = "", CancellationToken cancellationToken = default)
    {
        InMemoryFluNetBlobStore.ValidatePrefix(prefix);
        UriBuilder builder = new(baseUri);
        builder.Query = builder.Query.TrimStart('?') + (string.IsNullOrEmpty(builder.Query.TrimStart('?')) ? "" : "&") +
            "prefix=" + Uri.EscapeDataString(prefix);
        Uri uri = builder.Uri;
        policy.EnsureNetworkAccess(uri);
        using HttpRequestMessage request = new(HttpMethod.Get, uri);
        ApplyAuthentication(request);
        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
        JsonElement value = document.RootElement;
        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("keys", out JsonElement keys)) value = keys;
        if (value.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Blob list response must be a JSON array or an object with a 'keys' array.");
        return value.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
    }

    private void ApplyAuthentication(HttpRequestMessage request)
    {
        if (credential is not null) authentication.Apply(request, credential);
    }

    private Uri Resolve(string key)
    {
        InMemoryFluNetBlobStore.ValidateKey(key);
        string relative = string.Join(
            "/",
            key.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));
        return new Uri(baseUri, relative);
    }
}

public sealed record S3FluNetCredentials(
    SecretValue AccessKeyId,
    SecretValue SecretAccessKey,
    SecretValue? SessionToken = null);

/// <summary>
/// AWS Signature Version 4 object-store adapter. It is intentionally endpoint
/// based, so the same provider works with AWS S3, MinIO and S3-compatible
/// gateways without a vendor SDK.
/// </summary>
public sealed class S3FluNetBlobStore : IFluNetBlobStore
{
    private readonly Uri endpoint;
    private readonly string bucket;
    private readonly string region;
    private readonly HttpClient client;
    private readonly IExecutionPolicy policy;
    private readonly S3FluNetCredentials credentials;
    private readonly string service;

    public S3FluNetBlobStore(
        Uri endpoint,
        string bucket,
        string region,
        HttpClient client,
        IExecutionPolicy policy,
        S3FluNetCredentials credentials,
        string service = "s3")
    {
        if (endpoint is null || !endpoint.IsAbsoluteUri || endpoint.Scheme is not ("http" or "https"))
            throw new ArgumentException("S3 endpoint must be an absolute HTTP(S) URI.", nameof(endpoint));
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        ArgumentException.ThrowIfNullOrWhiteSpace(service);
        this.endpoint = endpoint.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? new Uri(endpoint.AbsoluteUri[..^1], UriKind.Absolute) : endpoint;
        this.bucket = bucket;
        this.region = region;
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
        this.credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        this.service = service;
    }

    public async ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        Uri uri = ObjectUri(key);
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get, uri, null, "", cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask PutAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        byte[] body = Encoding.UTF8.GetBytes(value);
        using HttpResponseMessage response = await SendAsync(HttpMethod.Put, ObjectUri(key), body, "text/plain; charset=utf-8", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async ValueTask<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Delete, ObjectUri(key), null, "", cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }

    public async ValueTask<IReadOnlyList<string>> ListAsync(string prefix = "", CancellationToken cancellationToken = default)
    {
        InMemoryFluNetBlobStore.ValidatePrefix(prefix);
        UriBuilder builder = new(ObjectUri(string.Empty));
        builder.Query = "list-type=2&prefix=" + Uri.EscapeDataString(prefix);
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get, builder.Uri, null, "", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        XDocument document = XDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        XNamespace ns = document.Root?.GetDefaultNamespace() ?? XNamespace.None;
        return document.Descendants(ns + "Key").Select(element => element.Value).Order(StringComparer.Ordinal).ToArray();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, Uri uri, byte[]? body, string contentType, CancellationToken cancellationToken)
    {
        policy.EnsureNetworkAccess(uri);
        byte[] payload = body ?? [];
        string payloadHash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string amzDate = now.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", System.Globalization.CultureInfo.InvariantCulture);
        string shortDate = now.ToUniversalTime().ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        Dictionary<string, string> headers = new(StringComparer.Ordinal)
        {
            ["host"] = uri.Authority,
            ["x-amz-content-sha256"] = payloadHash,
            ["x-amz-date"] = amzDate
        };
        if (credentials.SessionToken is not null) headers["x-amz-security-token"] = credentials.SessionToken.Reveal();
        if (body is not null) headers["content-type"] = contentType;
        string signedHeaders = string.Join(';', headers.Keys.Order(StringComparer.Ordinal));
        string canonicalHeaders = string.Join("\n", headers.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}:{NormalizeHeader(pair.Value)}")) + "\n";
        string canonicalRequest = string.Join("\n", method.Method, uri.AbsolutePath, uri.Query.TrimStart('?'), canonicalHeaders, signedHeaders, payloadHash);
        string scope = $"{shortDate}/{region}/{service}/aws4_request";
        string stringToSign = string.Join("\n", "AWS4-HMAC-SHA256", amzDate, scope, Hex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest))));
        byte[] signingKey = Hmac(Hmac(Hmac(Hmac(Encoding.UTF8.GetBytes("AWS4" + credentials.SecretAccessKey.Reveal()), shortDate), region), service), "aws4_request");
        string signature = Hex(Hmac(signingKey, stringToSign));
        using HttpRequestMessage request = new(method, uri);
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        if (credentials.SessionToken is not null) request.Headers.TryAddWithoutValidation("x-amz-security-token", credentials.SessionToken.Reveal());
        request.Headers.TryAddWithoutValidation("Authorization", $"AWS4-HMAC-SHA256 Credential={credentials.AccessKeyId.Reveal()}/{scope}, SignedHeaders={signedHeaders}, Signature={signature}");
        if (body is not null) request.Content = new ByteArrayContent(body);
        if (request.Content is not null) request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType.Split(';')[0]);
        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
    }

    private Uri ObjectUri(string key)
    {
        InMemoryFluNetBlobStore.ValidateKey(key.Length == 0 ? "placeholder" : key);
        string encodedKey = string.Join('/', key.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
        return new Uri(endpoint, $"{Uri.EscapeDataString(bucket)}/{encodedKey}");
    }

    private static string NormalizeHeader(string value) => string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static byte[] Hmac(byte[] key, string value) => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(value));
    private static string Hex(byte[] value) => Convert.ToHexString(value).ToLowerInvariant();
}

public sealed class BlobStorageCapabilityProvider : ICapabilityProvider
{
    public CapabilityDescriptor Descriptor { get; } = new(
        "storage.blob",
        "1.0",
        [FluNetPlatform.Any],
        ["storage.blob.read", "storage.blob.write", "storage.blob.delete"]);

    public bool IsAvailable => true;
}
