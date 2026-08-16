using System.Net.Http.Headers;
using System.Text;

namespace FluNET.Capabilities;

public interface IExecutionPolicy { void EnsureFileAccess(string path); void EnsureNetworkAccess(Uri uri); }
public sealed class AllowAllExecutionPolicy : IExecutionPolicy { public void EnsureFileAccess(string path) { } public void EnsureNetworkAccess(Uri uri) { } }

public sealed class RestrictedExecutionPolicy : IExecutionPolicy
{
    private readonly string[] roots;
    private readonly HashSet<string> hosts;
    private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    public RestrictedExecutionPolicy(IEnumerable<string> fileRoots, IEnumerable<string> networkHosts)
    {
        roots = fileRoots.Select(Path.GetFullPath).Select(path => path.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar).ToArray();
        hosts = new(networkHosts, StringComparer.OrdinalIgnoreCase);
    }
    public void EnsureFileAccess(string path)
    {
        string fullPath = Path.GetFullPath(path);
        bool allowed = roots.Any(root => fullPath.Equals(root.TrimEnd(Path.DirectorySeparatorChar), PathComparison) || fullPath.StartsWith(root, PathComparison));
        if (!allowed) throw new CapabilityDeniedException($"File access is not allowed: {fullPath}");
    }
    public void EnsureNetworkAccess(Uri uri)
    {
        if (uri.Scheme is not ("http" or "https") || !hosts.Contains(uri.Host)) throw new CapabilityDeniedException($"Network access is not allowed: {uri}");
    }
}

public interface IFluNetFileSystem
{
    Task<string[]> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default);
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);
    Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default);
    Task WriteAllBytesAsync(string path, byte[] content, CancellationToken cancellationToken = default);
    ValueTask<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default);
    ValueTask DeleteFileAsync(string path, CancellationToken cancellationToken = default);
}

public sealed class PhysicalFluNetFileSystem(IExecutionPolicy policy) : IFluNetFileSystem
{
    public Task<string[]> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default)
    { policy.EnsureFileAccess(path); return File.ReadAllLinesAsync(path, cancellationToken); }

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    { policy.EnsureFileAccess(path); return File.ReadAllTextAsync(path, cancellationToken); }

    public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default) =>
        AtomicWriteAsync(path, async stream =>
        {
            await using StreamWriter writer = new(stream, new UTF8Encoding(false), 16 * 1024, leaveOpen: true);
            await writer.WriteAsync(content.AsMemory(), cancellationToken).ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    public Task WriteAllBytesAsync(string path, byte[] content, CancellationToken cancellationToken = default) =>
        AtomicWriteAsync(path, stream => stream.WriteAsync(content, cancellationToken).AsTask(), cancellationToken);

    public ValueTask<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); policy.EnsureFileAccess(path); return ValueTask.FromResult(File.Exists(path)); }

    public ValueTask DeleteFileAsync(string path, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); policy.EnsureFileAccess(path); File.Delete(path); return ValueTask.CompletedTask; }

    private async Task AtomicWriteAsync(string path, Func<FileStream, Task> write, CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(path);
        policy.EnsureFileAccess(fullPath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory ?? Directory.GetCurrentDirectory(), "." + Path.GetFileName(fullPath) + ".flunet-" + Guid.NewGuid().ToString("N") + ".tmp");
        policy.EnsureFileAccess(temporary);
        try
        {
            await using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 16 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await write(stream).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

public sealed record HttpResourceResponse(byte[] Content, int StatusCode, string? MediaType, string? Charset, IReadOnlyDictionary<string, string[]> Headers);

public interface IHttpTransport
{
    Task<byte[]> GetBytesAsync(Uri uri, CancellationToken cancellationToken = default);
    async Task<HttpResourceResponse> GetResponseAsync(Uri uri, CancellationToken cancellationToken = default) =>
        await GetAsync(uri, cancellationToken).ConfigureAwait(false);
    async Task<HttpResourceResponse> GetAsync(Uri uri, CancellationToken cancellationToken = default) =>
        new(await GetBytesAsync(uri, cancellationToken).ConfigureAwait(false), 200, null, null, new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase));
    Task<string> PostJsonAsync(Uri uri, string json, CancellationToken cancellationToken = default);
    Task<string> PutJsonAsync(Uri uri, string json, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This HTTP provider does not support JSON PUT.");
    Task<string> PatchJsonAsync(Uri uri, string json, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This HTTP provider does not support JSON PATCH.");
    Task<string> DeleteAsync(Uri uri, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This HTTP provider does not support HTTP DELETE.");
}

public sealed class HttpTransport(HttpClient client, IExecutionPolicy policy) : IHttpTransport
{
    public async Task<HttpResourceResponse> GetResponseAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        policy.EnsureNetworkAccess(uri);
        using HttpRequestMessage request = new(HttpMethod.Get, uri);
        using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        byte[] content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<string, string[]> headers = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers) headers[header.Key] = header.Value.ToArray();
        foreach (KeyValuePair<string, IEnumerable<string>> header in response.Content.Headers) headers[header.Key] = header.Value.ToArray();
        MediaTypeHeaderValue? type = response.Content.Headers.ContentType;
        return new(content, (int)response.StatusCode, type?.MediaType, type?.CharSet, headers);
    }
    public async Task<HttpResourceResponse> GetAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        HttpResourceResponse response = await GetResponseAsync(uri, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(uri, response.StatusCode);
        return response;
    }
    public async Task<byte[]> GetBytesAsync(Uri uri, CancellationToken cancellationToken = default) => (await GetAsync(uri, cancellationToken).ConfigureAwait(false)).Content;
    public async Task<string> PostJsonAsync(Uri uri, string json, CancellationToken cancellationToken = default)
    {
        policy.EnsureNetworkAccess(uri);
        using StringContent content = new(json, Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await client.PostAsync(uri, content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<string> PutJsonAsync(Uri uri, string json, CancellationToken cancellationToken = default) =>
        SendJsonMutationAsync(HttpMethod.Put, uri, json, cancellationToken);

    public Task<string> PatchJsonAsync(Uri uri, string json, CancellationToken cancellationToken = default) =>
        SendJsonMutationAsync(new HttpMethod("PATCH"), uri, json, cancellationToken);

    public Task<string> DeleteAsync(Uri uri, CancellationToken cancellationToken = default) =>
        SendJsonMutationAsync(HttpMethod.Delete, uri, null, cancellationToken);

    private async Task<string> SendJsonMutationAsync(HttpMethod method, Uri uri, string? json, CancellationToken cancellationToken)
    {
        policy.EnsureNetworkAccess(uri);
        using HttpRequestMessage request = new(method, uri);
        if (json is not null)
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureSuccess(Uri uri, int statusCode)
    {
        if (statusCode is >= 200 and < 300) return;
        throw new HttpRequestException($"HTTP request to '{uri}' returned status {statusCode}.", null, (System.Net.HttpStatusCode)statusCode);
    }
}

public interface ITextOutput { ValueTask WriteLineAsync(string message, CancellationToken cancellationToken = default); }
public interface IEmailTransport { ValueTask<string> SendAsync(string recipient, string message, CancellationToken cancellationToken = default); }
public sealed class DiagnosticEmailTransport : IEmailTransport
{ public ValueTask<string> SendAsync(string recipient, string message, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult($"Email sent to {recipient}"); } }
public sealed class ConsoleTextOutput : ITextOutput
{ public ValueTask WriteLineAsync(string message, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); Console.WriteLine(message); return ValueTask.CompletedTask; } }
public sealed class CapabilityDeniedException : InvalidOperationException { public CapabilityDeniedException(string message) : base(message) { } }
