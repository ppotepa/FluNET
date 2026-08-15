using FluNET.Capabilities;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FluNET.Declarative.Reconciliation;

public enum ReconciliationCheckpointPhase
{
    Recovering,
    Observing,
    Diffed,
    Applying,
    Applied,
    BaselineSaved,
    Failed
}

public sealed record ReconciliationCheckpoint(
    Guid RunId,
    string DefinitionId,
    ReconciliationCheckpointPhase Phase,
    DateTimeOffset Timestamp,
    long? FencingToken = null,
    int? Creates = null,
    int? Updates = null,
    int? Deletes = null,
    int? Conflicts = null,
    string? Message = null)
{
    public bool IsTerminal => Phase is ReconciliationCheckpointPhase.BaselineSaved or ReconciliationCheckpointPhase.Failed;
}

public interface IReconciliationCheckpointStore
{
    ValueTask AppendAsync(ReconciliationCheckpoint checkpoint, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<ReconciliationCheckpoint>> ReadAsync(string definitionId, CancellationToken cancellationToken = default);
}

public sealed class InMemoryReconciliationCheckpointStore : IReconciliationCheckpointStore
{
    private readonly ConcurrentDictionary<string, List<ReconciliationCheckpoint>> items = new(StringComparer.Ordinal);

    public ValueTask AppendAsync(ReconciliationCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<ReconciliationCheckpoint> list = items.GetOrAdd(checkpoint.DefinitionId, _ => []);
        lock (list) list.Add(checkpoint);
        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<ReconciliationCheckpoint>> ReadAsync(string definitionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!items.TryGetValue(definitionId, out List<ReconciliationCheckpoint>? list))
            return ValueTask.FromResult<IReadOnlyList<ReconciliationCheckpoint>>(Array.Empty<ReconciliationCheckpoint>());
        lock (list) return ValueTask.FromResult<IReadOnlyList<ReconciliationCheckpoint>>(list.ToArray());
    }
}

public sealed record DurableReconciliationCheckpointOptions(string Directory);

/// <summary>Checksummed append-only reconciliation phase journal for crash/restart diagnosis and recovery.</summary>
public sealed class DurableReconciliationCheckpointStore(
    DurableReconciliationCheckpointOptions options,
    IExecutionPolicy policy) : IReconciliationCheckpointStore
{
    private static readonly JsonSerializerOptions CheckpointJson = CreateCheckpointJson();
    private readonly string directory = Path.GetFullPath(options?.Directory ?? throw new ArgumentNullException(nameof(options)));
    private readonly ConcurrentDictionary<string, SemaphoreSlim> gates = new(StringComparer.Ordinal);

    public async ValueTask AppendAsync(ReconciliationCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        string path = PathFor(checkpoint.DefinitionId);
        policy.EnsureFileAccess(path);
        Directory.CreateDirectory(directory);
        SemaphoreSlim gate = gates.GetOrAdd(checkpoint.DefinitionId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string payload = JsonSerializer.Serialize(checkpoint, CheckpointJson);
            string line = JsonSerializer.Serialize(new Envelope(payload, Checksum(payload))) + "\n";
            byte[] bytes = Encoding.UTF8.GetBytes(line);
            await using FileStream stream = new(path, FileMode.Append, FileAccess.Write, FileShare.Read, 16 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough);
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            stream.Flush(flushToDisk: true);
        }
        finally { gate.Release(); }
    }

    public async ValueTask<IReadOnlyList<ReconciliationCheckpoint>> ReadAsync(string definitionId, CancellationToken cancellationToken = default)
    {
        string path = PathFor(definitionId);
        policy.EnsureFileAccess(path);
        SemaphoreSlim gate = gates.GetOrAdd(definitionId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(path)) return Array.Empty<ReconciliationCheckpoint>();
            string[] lines = await File.ReadAllLinesAsync(path, cancellationToken).ConfigureAwait(false);
            List<ReconciliationCheckpoint> result = [];
            for (int index = 0; index < lines.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(lines[index])) continue;
                try
                {
                    Envelope envelope = JsonSerializer.Deserialize<Envelope>(lines[index]) ?? throw new JsonException("Null envelope.");
                    if (!FixedEquals(envelope.Checksum, Checksum(envelope.Payload)))
                        throw new InvalidDataException($"Reconciliation checkpoint '{path}' failed checksum validation at line {index + 1}.");
                    ReconciliationCheckpoint item = JsonSerializer.Deserialize<ReconciliationCheckpoint>(envelope.Payload, CheckpointJson) ?? throw new JsonException("Null checkpoint.");
                    if (!item.DefinitionId.Equals(definitionId, StringComparison.Ordinal))
                        throw new InvalidDataException($"Reconciliation checkpoint '{path}' contains another definition id.");
                    result.Add(item);
                }
                catch (JsonException exception)
                {
                    throw new InvalidDataException($"Reconciliation checkpoint '{path}' is corrupt at line {index + 1}.", exception);
                }
            }
            return result;
        }
        finally { gate.Release(); }
    }

    private string PathFor(string definitionId)
    {
        string name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(definitionId))).ToLowerInvariant();
        return Path.Combine(directory, name + ".checkpoint.jsonl");
    }

    private static JsonSerializerOptions CreateCheckpointJson()
    {
        JsonSerializerOptions options = new();
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static string Checksum(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static bool FixedEquals(string left, string right)
    {
        try { return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(left), Convert.FromHexString(right)); }
        catch (FormatException) { return false; }
    }
    private sealed record Envelope(string Payload, string Checksum);
}

public static class ReconciliationRuntimeServiceCollectionExtensions
{
    public static IServiceCollection AddDurableFluNetReconciliationRuntime(
        this IServiceCollection services,
        string directory,
        TimeSpan? leaseTtl = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        string root = Path.GetFullPath(directory);
        services.AddDurableFluNetReconciliationState(Path.Combine(root, "state"));
        services.AddDurableFluNetReconciliationCoordination(Path.Combine(root, "leases"), leaseTtl);
        services.AddSingleton(new DurableReconciliationCheckpointOptions(Path.Combine(root, "checkpoints")));
        services.AddSingleton<IReconciliationCheckpointStore, DurableReconciliationCheckpointStore>();
        return services;
    }
}
