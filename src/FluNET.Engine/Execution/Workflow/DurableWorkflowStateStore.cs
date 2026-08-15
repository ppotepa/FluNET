using FluNET.Capabilities;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FluNET.Execution.Workflow;

public sealed record DurableWorkflowStoreOptions(string Directory);

/// <summary>
/// Checksummed append-only workflow journal for durable single-host execution.
/// A run has one writer; FileShare.Read makes accidental cross-process writers fail fast.
/// </summary>
public sealed class DurableWorkflowStateStore : IWorkflowStateStore
{
    private static readonly JsonSerializerOptions EventJsonOptions = CreateEventJsonOptions();
    private readonly string _directory;
    private readonly IExecutionPolicy _policy;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = [];

    public DurableWorkflowStateStore(DurableWorkflowStoreOptions options, IExecutionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(options);
        _directory = Path.GetFullPath(options.Directory ?? throw new ArgumentNullException(nameof(options.Directory)));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public async ValueTask AppendAsync(WorkflowEvent item, CancellationToken cancellationToken = default)
    {
        string path = PathFor(item.RunId);
        _policy.EnsureFileAccess(path);
        Directory.CreateDirectory(_directory);
        SemaphoreSlim gate = _locks.GetOrAdd(item.RunId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string eventJson = JsonSerializer.Serialize(item, EventJsonOptions);
            JournalEnvelope envelope = new(eventJson, Checksum(eventJson));
            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope) + "\n");
            await using FileStream stream = new(
                path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                16 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            stream.Flush(flushToDisk: true);
        }
        finally { gate.Release(); }
    }

    public async ValueTask<IReadOnlyList<WorkflowEvent>> ReadAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        string path = PathFor(runId);
        _policy.EnsureFileAccess(path);
        SemaphoreSlim gate = _locks.GetOrAdd(runId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(path)) return Array.Empty<WorkflowEvent>();
            string[] lines = await File.ReadAllLinesAsync(path, cancellationToken).ConfigureAwait(false);
            List<WorkflowEvent> result = [];
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index];
                if (string.IsNullOrWhiteSpace(line)) continue;
                JournalEnvelope envelope;
                try { envelope = JsonSerializer.Deserialize<JournalEnvelope>(line) ?? throw new JsonException("Null envelope."); }
                catch (JsonException exception) { throw Corrupt(path, index + 1, "invalid JSON envelope", exception); }
                if (!CryptographicOperations.FixedTimeEquals(
                        Convert.FromHexString(envelope.Checksum),
                        Convert.FromHexString(Checksum(envelope.EventJson))))
                    throw Corrupt(path, index + 1, "checksum mismatch");
                WorkflowEvent item;
                try { item = JsonSerializer.Deserialize<WorkflowEvent>(envelope.EventJson, EventJsonOptions) ?? throw new JsonException("Null event."); }
                catch (JsonException exception) { throw Corrupt(path, index + 1, "invalid workflow event", exception); }
                if (item.RunId != runId) throw Corrupt(path, index + 1, $"run id {item.RunId} does not match file run id {runId}");
                result.Add(item);
            }
            return result;
        }
        finally { gate.Release(); }
    }

    private string PathFor(Guid runId) => Path.Combine(_directory, $"{runId:N}.journal.jsonl");
    private static JsonSerializerOptions CreateEventJsonOptions()
    {
        JsonSerializerOptions options = new();
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
    private static string Checksum(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static InvalidDataException Corrupt(string path, int line, string reason, Exception? inner = null) =>
        new($"Workflow journal '{path}' is corrupt at line {line}: {reason}.", inner);
    private sealed record JournalEnvelope(string EventJson, string Checksum);
}
