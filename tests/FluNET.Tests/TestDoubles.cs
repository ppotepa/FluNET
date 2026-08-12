using System.Text;
using FluNET.Capabilities;

namespace FluNET.Tests;

internal sealed class FakeHttpTransport : IHttpTransport
{
    private readonly Dictionary<Uri, byte[]> _downloads = new();

    public List<(Uri Uri, string Json)> Posts { get; } = [];
    public string PostResponse { get; set; } = "{\"status\":\"accepted\"}";
    public Exception? Failure { get; set; }
    public bool WaitForCancellation { get; set; }

    public void AddDownload(string uri, string content) =>
        AddDownload(uri, Encoding.UTF8.GetBytes(content));

    public void AddDownload(string uri, byte[] content) =>
        _downloads[new Uri(uri)] = content;

    public async Task<byte[]> GetBytesAsync(
        Uri uri,
        CancellationToken cancellationToken = default)
    {
        if (WaitForCancellation)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        if (Failure is not null)
        {
            throw Failure;
        }

        return _downloads.TryGetValue(uri, out byte[]? content)
            ? content
            : throw new HttpRequestException($"No fake response configured for '{uri}'.");
    }

    public Task<string> PostJsonAsync(
        Uri uri,
        string json,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Failure is not null)
        {
            throw Failure;
        }

        Posts.Add((uri, json));
        return Task.FromResult(PostResponse);
    }
}

internal sealed class RecordingTextOutput : ITextOutput
{
    public List<string> Lines { get; } = [];

    public ValueTask WriteLineAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Lines.Add(message);
        return ValueTask.CompletedTask;
    }
}
