using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;

namespace FluNET.Capabilities;

public sealed record SecureHostOptions(
    IReadOnlyList<string> FileRoots,
    IReadOnlyList<string> NetworkHosts,
    bool AllowHttp = false,
    bool AllowPrivateAddresses = false,
    int MaxRedirects = 5);

/// <summary>
/// Production-oriented policy. Unlike the compatibility policy it resolves existing symlink
/// segments before root checks and requires an explicit host allow-list.
/// </summary>
public sealed class SecureExecutionPolicy : IExecutionPolicy
{
    private readonly string[] roots;
    private readonly HashSet<string> hosts;
    private readonly SecureHostOptions options;
    private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public SecureExecutionPolicy(SecureHostOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        if (options.MaxRedirects < 0 || options.MaxRedirects > 20) throw new ArgumentOutOfRangeException(nameof(options.MaxRedirects));
        roots = options.FileRoots.Select(CanonicalizePath).Select(path => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).Distinct(PathComparer).ToArray();
        if (roots.Length == 0) throw new ArgumentException("Secure host requires at least one file root.", nameof(options));
        hosts = new(options.NetworkHosts.Select(NormalizeHost), StringComparer.OrdinalIgnoreCase);
    }

    public int MaxRedirects => options.MaxRedirects;

    public void EnsureFileAccess(string path)
    {
        string canonical = CanonicalizePath(path);
        bool allowed = roots.Any(root => canonical.Equals(root, PathComparison) || canonical.StartsWith(root + Path.DirectorySeparatorChar, PathComparison));
        if (!allowed) throw new CapabilityDeniedException($"File access escapes configured secure roots: {canonical}");
    }

    public void EnsureNetworkAccess(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || uri.Scheme is not ("http" or "https")) throw new CapabilityDeniedException($"Only absolute HTTP(S) endpoints are allowed: {uri}");
        if (!options.AllowHttp && uri.Scheme != "https") throw new CapabilityDeniedException($"Plain HTTP is disabled by secure host policy: {uri}");
        string host = NormalizeHost(uri.DnsSafeHost);
        if (!hosts.Contains(host)) throw new CapabilityDeniedException($"Network host is not allow-listed: {host}");
        if (IPAddress.TryParse(host, out IPAddress? literal)) EnsureAddressAccess(host, literal);
    }

    public async ValueTask EnsureNetworkEndpointAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        EnsureNetworkAccess(uri);
        if (options.AllowPrivateAddresses) return;
        string host = NormalizeHost(uri.DnsSafeHost);
        if (IPAddress.TryParse(host, out IPAddress? literal))
        {
            EnsureAddressAccess(host, literal);
            return;
        }
        IPAddress[] addresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
        if (addresses.Length == 0) throw new CapabilityDeniedException($"Network host did not resolve: {host}");
        foreach (IPAddress address in addresses) EnsureAddressAccess(host, address);
    }

    /// <summary>Validates an already-resolved address at the actual connection boundary.</summary>
    public void EnsureAddressAccess(string host, IPAddress address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentNullException.ThrowIfNull(address);
        if (!options.AllowPrivateAddresses && IsPrivateOrNonPublic(address))
            throw new CapabilityDeniedException($"Private or non-public network address is disabled for '{host}': {address}");
    }

    private static string CanonicalizePath(string path)
    {
        string full = Path.GetFullPath(path);
        string root = Path.GetPathRoot(full) ?? throw new InvalidOperationException($"Path has no root: {full}");
        string current = root;
        string relative = full[root.Length..];
        foreach (string part in relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(current, part);
            FileSystemInfo? info = Directory.Exists(candidate) ? new DirectoryInfo(candidate) : File.Exists(candidate) ? new FileInfo(candidate) : null;
            if (info?.LinkTarget is not null)
            {
                FileSystemInfo? target = info.ResolveLinkTarget(returnFinalTarget: true);
                current = Path.GetFullPath(target?.FullName ?? candidate);
            }
            else current = candidate;
        }
        return Path.GetFullPath(current).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool IsPrivateOrNonPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return true;
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            byte[] b = address.GetAddressBytes();
            return b[0] == 0 ||
                   b[0] == 10 ||
                   b[0] == 100 && b[1] is >= 64 and <= 127 ||
                   b[0] == 127 ||
                   b[0] == 169 && b[1] == 254 ||
                   b[0] == 172 && b[1] is >= 16 and <= 31 ||
                   b[0] == 192 && b[1] == 0 && b[2] == 0 ||
                   b[0] == 192 && b[1] == 168 ||
                   b[0] == 198 && b[1] is 18 or 19 ||
                   b[0] >= 224;
        }
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            byte[] b = address.GetAddressBytes();
            return address.Equals(IPAddress.IPv6Any) ||
                   address.Equals(IPAddress.IPv6Loopback) ||
                   address.IsIPv6Multicast ||
                   (b[0] & 0xFE) == 0xFC ||
                   b[0] == 0xFE && (b[1] & 0xC0) is 0x80 or 0xC0;
        }
        return true;
    }

    private static string NormalizeHost(string host) => host.Trim().TrimEnd('.').ToLowerInvariant();
    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}

/// <summary>
/// HTTP capability with redirects disabled at the handler. Every redirect target is re-authorized,
/// and the production handler validates DNS results again at the socket connection boundary.
/// </summary>
public sealed class SecureHttpTransport : IHttpTransport, IAuthenticatedHttpTransport, IDisposable
{
    private readonly SecureExecutionPolicy policy;
    private readonly IHttpAuthenticationScheme authentication;
    private readonly HttpMessageInvoker invoker;

    public SecureHttpTransport(SecureExecutionPolicy policy, IHttpAuthenticationScheme authentication)
        : this(policy, authentication, CreateDefaultHandler(policy), ownsHandler: true)
    {
    }

    public SecureHttpTransport(SecureExecutionPolicy policy, IHttpAuthenticationScheme authentication, HttpMessageHandler handler, bool ownsHandler = false)
    {
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
        this.authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        invoker = new HttpMessageInvoker(handler ?? throw new ArgumentNullException(nameof(handler)), disposeHandler: ownsHandler);
    }

    public async Task<byte[]> GetBytesAsync(Uri uri, CancellationToken cancellationToken = default) =>
        (await GetAsync(uri, cancellationToken).ConfigureAwait(false)).Content;

    public Task<HttpResourceResponse> GetAsync(Uri uri, CancellationToken cancellationToken = default) =>
        SendGetAsync(uri, null, cancellationToken);

    public Task<HttpResourceResponse> GetAsync(Uri uri, SecretValue credential, CancellationToken cancellationToken = default) =>
        SendGetAsync(uri, credential ?? throw new ArgumentNullException(nameof(credential)), cancellationToken);

    public async Task<string> PostJsonAsync(Uri uri, string json, CancellationToken cancellationToken = default)
    {
        await policy.EnsureNetworkEndpointAsync(uri, cancellationToken).ConfigureAwait(false);
        using HttpRequestMessage request = new(HttpMethod.Post, uri)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        using HttpResponseMessage response = await invoker.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (IsRedirect(response.StatusCode)) throw new CapabilityDeniedException("Secure HTTP does not follow mutation redirects. Resolve the final POST endpoint explicitly.");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResourceResponse> SendGetAsync(Uri initial, SecretValue? credential, CancellationToken cancellationToken)
    {
        Uri current = initial;
        for (int redirect = 0; ; redirect++)
        {
            await policy.EnsureNetworkEndpointAsync(current, cancellationToken).ConfigureAwait(false);
            using HttpRequestMessage request = new(HttpMethod.Get, current);
            if (credential is not null) authentication.Apply(request, credential);
            using HttpResponseMessage response = await invoker.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (IsRedirect(response.StatusCode))
            {
                if (redirect >= policy.MaxRedirects) throw new CapabilityDeniedException($"HTTP redirect limit exceeded for {initial}.");
                Uri? next = RedirectTarget(current, response.Headers.Location);
                if (next is null) throw new InvalidDataException("HTTP redirect response has no valid Location header.");
                if (credential is not null && !SameOrigin(current, next))
                    throw new CapabilityDeniedException("Authenticated HTTP redirect attempted to change origin; credential forwarding was blocked.");
                current = next;
                continue;
            }

            byte[] content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            Dictionary<string, string[]> headers = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers) headers[header.Key] = header.Value.ToArray();
            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Content.Headers) headers[header.Key] = header.Value.ToArray();
            response.EnsureSuccessStatusCode();
            MediaTypeHeaderValue? type = response.Content.Headers.ContentType;
            return new(content, (int)response.StatusCode, type?.MediaType, type?.CharSet, headers);
        }
    }

    private static SocketsHttpHandler CreateDefaultHandler(SecureExecutionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectCallback = (context, cancellationToken) => ConnectValidatedAsync(policy, context.DnsEndPoint, cancellationToken)
        };
    }

    private static async ValueTask<Stream> ConnectValidatedAsync(
        SecureExecutionPolicy policy,
        DnsEndPoint endpoint,
        CancellationToken cancellationToken)
    {
        IPAddress[] addresses;
        if (IPAddress.TryParse(endpoint.Host, out IPAddress? literal)) addresses = [literal];
        else addresses = await Dns.GetHostAddressesAsync(endpoint.Host, cancellationToken).ConfigureAwait(false);
        if (addresses.Length == 0) throw new CapabilityDeniedException($"Network host did not resolve at connect time: {endpoint.Host}");

        foreach (IPAddress address in addresses) policy.EnsureAddressAccess(endpoint.Host, address);

        Exception? lastFailure = null;
        foreach (IPAddress address in addresses)
        {
            Socket socket = new(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, endpoint.Port), cancellationToken).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (OperationCanceledException)
            {
                socket.Dispose();
                throw;
            }
            catch (SocketException exception)
            {
                socket.Dispose();
                lastFailure = exception;
            }
        }
        throw new HttpRequestException($"Unable to connect to any validated address for '{endpoint.Host}'.", lastFailure);
    }

    private static Uri? RedirectTarget(Uri current, Uri? location) => location is null ? null : location.IsAbsoluteUri ? location : new Uri(current, location);
    private static bool SameOrigin(Uri left, Uri right) => left.Scheme.Equals(right.Scheme, StringComparison.OrdinalIgnoreCase) && left.Host.Equals(right.Host, StringComparison.OrdinalIgnoreCase) && left.Port == right.Port;
    private static bool IsRedirect(HttpStatusCode status) => (int)status is 301 or 302 or 303 or 307 or 308;

    public void Dispose() => invoker.Dispose();
}

public static class SecureFluNetHostServiceCollectionExtensions
{
    public static IServiceCollection AddSecureFluNetHost(
        this IServiceCollection services,
        IEnumerable<string> fileRoots,
        IEnumerable<string> networkHosts,
        bool allowHttp = false,
        bool allowPrivateAddresses = false,
        int maxRedirects = 5)
    {
        ArgumentNullException.ThrowIfNull(services);
        SecureHostOptions options = new(fileRoots.ToArray(), networkHosts.ToArray(), allowHttp, allowPrivateAddresses, maxRedirects);
        services.RemoveAll<IExecutionPolicy>();
        services.RemoveAll<IHttpTransport>();
        services.RemoveAll<IAuthenticatedHttpTransport>();
        services.AddSingleton(options);
        services.AddSingleton<SecureExecutionPolicy>();
        services.AddSingleton<IExecutionPolicy>(provider => provider.GetRequiredService<SecureExecutionPolicy>());
        services.AddSingleton<SecureHttpTransport>();
        services.AddSingleton<IHttpTransport>(provider => provider.GetRequiredService<SecureHttpTransport>());
        services.AddSingleton<IAuthenticatedHttpTransport>(provider => provider.GetRequiredService<SecureHttpTransport>());
        return services;
    }
}
