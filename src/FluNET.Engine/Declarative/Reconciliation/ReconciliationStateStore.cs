using FluNET.Capabilities;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FluNET.Declarative.Reconciliation;

public sealed record ReconciliationBaselineState(
    string DefinitionId,
    string TargetScheme,
    string TargetValue,
    string KeyField,
    JsonElement[] Records,
    DateTimeOffset StoredAt)
{
    public ObservedStateSnapshot ToSnapshot() => new(
        new ResourceIdentity(TargetScheme, TargetValue),
        KeyField,
        Records.Select(record => record.Clone()),
        StoredAt);

    public static ReconciliationBaselineState From(
        SyncDefinition definition,
        ResourceStateSnapshot converged)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(converged);
        ResourceIdentity target = ResourceIdentity.Parse(definition.Goal.TargetResource);
        return new(
            definition.Id,
            target.Scheme,
            target.Value,
            definition.Goal.KeyField,
            converged.Records.Select(record => record.Value.Clone()).ToArray(),
            DateTimeOffset.UtcNow);
    }
}

public interface IReconciliationStateStore
{
    ValueTask<ReconciliationBaselineState?> GetAsync(
        string definitionId,
        CancellationToken cancellationToken = default);

    ValueTask SetAsync(
        ReconciliationBaselineState state,
        CancellationToken cancellationToken = default);

    ValueTask RemoveAsync(
        string definitionId,
        CancellationToken cancellationToken = default);
}

public sealed class InMemoryReconciliationStateStore : IReconciliationStateStore
{
    private readonly ConcurrentDictionary<string, ReconciliationBaselineState> states =
        new(StringComparer.Ordinal);

    public ValueTask<ReconciliationBaselineState?> GetAsync(
        string definitionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        states.TryGetValue(definitionId, out ReconciliationBaselineState? value);
        return ValueTask.FromResult(value is null ? null : Clone(value));
    }

    public ValueTask SetAsync(
        ReconciliationBaselineState state,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        states[state.DefinitionId] = Clone(state);
        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveAsync(
        string definitionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        states.TryRemove(definitionId, out _);
        return ValueTask.CompletedTask;
    }

    private static ReconciliationBaselineState Clone(ReconciliationBaselineState value) => value with
    {
        Records = value.Records.Select(record => record.Clone()).ToArray()
    };
}

public sealed record DurableReconciliationStateOptions(string Directory);

/// <summary>
/// Checksummed, atomic single-host baseline store. Each SYNC definition owns one file.
/// This store persists only converged reconciliation state, never secret plaintext.
/// </summary>
public sealed class DurableReconciliationStateStore(
    DurableReconciliationStateOptions options,
    IExecutionPolicy policy) : IReconciliationStateStore
{
    private readonly string directory = Path.GetFullPath(
        options?.Directory ?? throw new ArgumentNullException(nameof(options)));
    private readonly ConcurrentDictionary<string, SemaphoreSlim> gates = new(StringComparer.Ordinal);

    public async ValueTask<ReconciliationBaselineState?> GetAsync(
        string definitionId,
        CancellationToken cancellationToken = default)
    {
        string path = PathFor(definitionId);
        policy.EnsureFileAccess(path);
        SemaphoreSlim gate = gates.GetOrAdd(definitionId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(path)) return null;
            string json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            Envelope envelope = JsonSerializer.Deserialize<Envelope>(json)
                ?? throw new InvalidDataException($"Reconciliation state '{path}' is empty.");
            string actual = Checksum(envelope.Payload);
            if (!FixedEquals(envelope.Checksum, actual))
                throw new InvalidDataException($"Reconciliation state '{path}' failed checksum validation.");
            ReconciliationBaselineState state = JsonSerializer.Deserialize<ReconciliationBaselineState>(envelope.Payload)
                ?? throw new InvalidDataException($"Reconciliation state '{path}' has no payload.");
            if (!state.DefinitionId.Equals(definitionId, StringComparison.Ordinal))
                throw new InvalidDataException($"Reconciliation state '{path}' belongs to '{state.DefinitionId}', expected '{definitionId}'.");
            return state with { Records = state.Records.Select(record => record.Clone()).ToArray() };
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Reconciliation state '{path}' is corrupt.", exception);
        }
        finally { gate.Release(); }
    }

    public async ValueTask SetAsync(
        ReconciliationBaselineState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        string path = PathFor(state.DefinitionId);
        policy.EnsureFileAccess(path);
        Directory.CreateDirectory(directory);
        SemaphoreSlim gate = gates.GetOrAdd(state.DefinitionId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        policy.EnsureFileAccess(temporary);
        try
        {
            string payload = JsonSerializer.Serialize(state);
            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Envelope(payload, Checksum(payload))));
            await using (FileStream stream = new(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                16 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            gate.Release();
        }
    }

    public async ValueTask RemoveAsync(
        string definitionId,
        CancellationToken cancellationToken = default)
    {
        string path = PathFor(definitionId);
        policy.EnsureFileAccess(path);
        SemaphoreSlim gate = gates.GetOrAdd(definitionId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path)) File.Delete(path);
        }
        finally { gate.Release(); }
    }

    private string PathFor(string definitionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
        string name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(definitionId))).ToLowerInvariant();
        return Path.Combine(directory, name + ".reconciliation.json");
    }

    private static string Checksum(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static bool FixedEquals(string left, string right)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(left),
                Convert.FromHexString(right));
        }
        catch (FormatException) { return false; }
    }

    private sealed record Envelope(string Payload, string Checksum);
}

public static class ReconciliationStateServiceCollectionExtensions
{
    public static IServiceCollection AddDurableFluNetReconciliationState(
        this IServiceCollection services,
        string directory)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(new DurableReconciliationStateOptions(directory));
        services.AddSingleton<IReconciliationStateStore, DurableReconciliationStateStore>();
        return services;
    }
}
