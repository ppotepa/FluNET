using System.Net.Http.Headers;
using System.Text;

namespace FluNET.Capabilities;

/// <summary>Authorizes external effects before they are performed.</summary>
public interface IExecutionPolicy
{
    void EnsureFileAccess(string path);
    void EnsureNetworkAccess(Uri uri);
}

/// <summary>Backward-compatible policy. Replace it in DI to constrain effects.</summary>
public sealed class AllowAllExecutionPolicy : IExecutionPolicy
{
    public void EnsureFileAccess(string path)
    {
    }

    public void EnsureNetworkAccess(Uri uri)
    {
    }
}

/// <summary>A policy that restricts files to configured roots and HTTP to configured hosts.</summary>
public sealed class RestrictedExecutionPolicy : IExecutionPolicy
{
    private readonly string[] _roots;
    private readonly HashSet<string> _hosts;
    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    public RestrictedExecutionPolicy(IEnumerable<string> fileRoots, IEnumerable<string> networkHosts)
    {
        _roots = fileRoots
            .Select(Path.GetFullPath)
            .Select(path => path.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar)
            .ToArray();
        _hosts = new HashSet<string>(networkHosts, StringComparer.OrdinalIgnoreCase);
    }

    public void EnsureFileAccess(string path)
    {
        string fullPath = Path.GetFullPath(path);
        bool allowed = _roots.Any(root =>
            fullPath.Equals(root.TrimEnd(Path.DirectorySeparatorChar), PathComparison) ||
            fullPath.StartsWith(root, PathComparison));

        if (!allowed)
        {
            throw new CapabilityDeniedException($"File access is not allowed: {fullPath}");
        }
    }

    public void EnsureNetworkAccess(Uri uri)
    {
        if (uri.Scheme is not ("http" or "https") || !_hosts.Contains(uri.Host))
        {
            throw new CapabilityDeniedException($"Network access is not allowed: {uri}");
        }
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
    {
        policy.EnsureFileAccess(path);
        return File.ReadAllLinesAsync(path, cancellationToken);
    }

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    {
        policy.EnsureFileAccess(path);
        return File.ReadAllTextAsync(path, cancellationToken);
    }

    public async Task WriteAllTextAsync(
        string path,
        string content,
        CancellationToken cancellationToken = default)
    {
        policy.EnsureFileAccess(path);
        EnsureDirectory(path);
        await File.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteAllBytesAsync(
        string path,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        policy.EnsureFileAccess(path);
        EnsureDirectory(path);
        await File.WriteAllBytesAsync(path, content, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        policy.EnsureFileAccess(path);
        return ValueTask.FromResult(File.Exists(path));
    }

    public ValueTask DeleteFileAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        policy.EnsureFileAccess(path);
        File.Delete(path);
        return ValueTask.CompletedTask;
    }

    private static void EnsureDirectory(string path)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}

public interface IHttpTransport
{
    Task<byte[]> GetBytesAsync(Uri uri, CancellationToken cancellationToken = default);
    Task<string> PostJsonAsync(Uri uri, string json, CancellationToken cancellationToken = default);
}

public sealed class HttpTransport(HttpClient client, IExecutionPolicy policy) : IHttpTransport
{
    public async Task<byte[]> GetBytesAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        policy.EnsureNetworkAccess(uri);
        return await client.GetByteArrayAsync(uri, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> PostJsonAsync(
        Uri uri,
        string json,
        CancellationToken cancellationToken = default)
    {
        policy.EnsureNetworkAccess(uri);
        using StringContent content = new(json, Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await client.PostAsync(uri, content, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }
}

public interface ITextOutput
{
    ValueTask WriteLineAsync(string message, CancellationToken cancellationToken = default);
}

public interface IEmailTransport
{
    ValueTask<string> SendAsync(
        string recipient,
        string message,
        CancellationToken cancellationToken = default);
}

/// <summary>Default deterministic email boundary; hosts can replace it with SMTP or an API.</summary>
public sealed class DiagnosticEmailTransport : IEmailTransport
{
    public ValueTask<string> SendAsync(
        string recipient,
        string message,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        System.Diagnostics.Debug.WriteLine($"Sending email to {recipient}");
        System.Diagnostics.Debug.WriteLine($"Message: {message}");
        return ValueTask.FromResult($"Email sent to {recipient}");
    }
}

public sealed class ConsoleTextOutput : ITextOutput
{
    public ValueTask WriteLineAsync(string message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Console.WriteLine(message);
        return ValueTask.CompletedTask;
    }
}

public sealed class CapabilityDeniedException : Exception
{
    public CapabilityDeniedException(string message) : base(message)
    {
    }
}

internal static class DefaultCapabilities
{
    private static readonly IExecutionPolicy Policy = new AllowAllExecutionPolicy();
    internal static readonly IFluNetFileSystem FileSystem = new PhysicalFluNetFileSystem(Policy);
    internal static readonly IHttpTransport Http = new HttpTransport(new HttpClient
    {
        Timeout = TimeSpan.FromMinutes(5)
    }, Policy);
    internal static readonly ITextOutput Output = new ConsoleTextOutput();
}
