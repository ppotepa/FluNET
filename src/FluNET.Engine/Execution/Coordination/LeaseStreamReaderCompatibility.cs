using System.Text;

namespace FluNET.Execution.Coordination;

/// <summary>
/// Keeps the lease-file reader call source-compatible while delegating to the
/// explicit five-argument System.IO.StreamReader overload used by .NET 8.
/// </summary>
internal sealed class StreamReader : IDisposable
{
    private readonly System.IO.StreamReader _inner;

    public StreamReader(Stream stream, Encoding encoding, bool leaveOpen)
    {
        _inner = new System.IO.StreamReader(
            stream,
            encoding,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 1024,
            leaveOpen: leaveOpen);
    }

    public string ReadToEnd() => _inner.ReadToEnd();

    public void Dispose() => _inner.Dispose();
}
