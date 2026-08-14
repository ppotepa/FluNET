using FluNET.Capabilities;
using FluNET.Execution.Planning;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FluNET.Declarative.Reconciliation;

public interface IReconciliationExecutor
{
    ValueTask<ReconciliationRunResult> RunAsync(
        SyncDefinition definition,
        ResourceStateSnapshot? baseline = null,
        CancellationToken cancellationToken = default);
}

public sealed record ReconciliationLease(
    string ResourceIdentity,
    string OwnerId,
    long FencingToken,
    DateTimeOffset ExpiresAt);

public interface IReconciliationLeaseStore
{
    ValueTask<ReconciliationLease?> TryAcquireAsync(
        string resourceIdentity,
        string ownerId,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);

    ValueTask<ReconciliationLease?> RenewAsync(
        ReconciliationLease lease,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);

    ValueTask ReleaseAsync(
        ReconciliationLease lease,
        CancellationToken cancellationToken = default);
}

public sealed class InMemoryReconciliationLeaseStore : IReconciliationLeaseStore
{
    private sealed class Slot
    {
        public readonly object Gate = new();
        public long LastToken;
        public ReconciliationLease? Current;
    }

    private readonly ConcurrentDictionary<string, Slot> slots = new(StringComparer.Ordinal);

    public ValueTask<ReconciliationLease?> TryAcquireAsync(string resourceIdentity, string ownerId, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Validate(resourceIdentity, ownerId, ttl);
        Slot slot = slots.GetOrAdd(resourceIdentity, _ => new Slot());
        lock (slot.Gate)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (slot.Current is not null && slot.Current.ExpiresAt > now) return ValueTask.FromResult<ReconciliationLease?>(null);
            ReconciliationLease lease = new(resourceIdentity, ownerId, checked(++slot.LastToken), now.Add(ttl));
            slot.Current = lease;
            return ValueTask.FromResult<ReconciliationLease?>(lease);
        }
    }

    public ValueTask<ReconciliationLease?> RenewAsync(ReconciliationLease lease, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!slots.TryGetValue(lease.ResourceIdentity, out Slot? slot)) return ValueTask.FromResult<ReconciliationLease?>(null);
        lock (slot.Gate)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (slot.Current is null || slot.Current.OwnerId != lease.OwnerId || slot.Current.FencingToken != lease.FencingToken || slot.Current.ExpiresAt <= now)
                return ValueTask.FromResult<ReconciliationLease?>(null);
            ReconciliationLease renewed = lease with { ExpiresAt = now.Add(ttl) };
            slot.Current = renewed;
            return ValueTask.FromResult<ReconciliationLease?>(renewed);
        }
    }

    public ValueTask ReleaseAsync(ReconciliationLease lease, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (slots.TryGetValue(lease.ResourceIdentity, out Slot? slot))
        {
            lock (slot.Gate)
                if (slot.Current?.OwnerId == lease.OwnerId && slot.Current.FencingToken == lease.FencingToken) slot.Current = null;
        }
        return ValueTask.CompletedTask;
    }

    private static void Validate(string identity, string owner, TimeSpan ttl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        if (ttl <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ttl));
    }
}

public sealed record DurableReconciliationLeaseOptions(string Directory);

/// <summary>
/// Shared-filesystem lease store. Exclusive file ownership serializes acquire/renew/release and
/// the persisted counter provides monotonic fencing tokens across process restarts.
/// </summary>
public sealed class DurableReconciliationLeaseStore(
    DurableReconciliationLeaseOptions options,
    IExecutionPolicy policy) : IReconciliationLeaseStore
{
    private readonly string directory = Path.GetFullPath(options?.Directory ?? throw new ArgumentNullException(nameof(options)));
    private readonly ConcurrentDictionary<string, SemaphoreSlim> localGates = new(StringComparer.Ordinal);

    public async ValueTask<ReconciliationLease?> TryAcquireAsync(string resourceIdentity, string ownerId, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        Validate(resourceIdentity, ownerId, ttl);
        return await WithExclusiveAsync(resourceIdentity, cancellationToken, state =>
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (state.OwnerId is not null && state.ExpiresAt is DateTimeOffset expiry && expiry > now)
                return (state, (ReconciliationLease?)null);
            long token = checked(state.LastFencingToken + 1);
            ReconciliationLease lease = new(resourceIdentity, ownerId, token, now.Add(ttl));
            return (new LeaseFileState(token, ownerId, lease.ExpiresAt), lease);
        }).ConfigureAwait(false);
    }

    public async ValueTask<ReconciliationLease?> RenewAsync(ReconciliationLease lease, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        if (ttl <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ttl));
        return await WithExclusiveAsync(lease.ResourceIdentity, cancellationToken, state =>
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (state.OwnerId != lease.OwnerId || state.LastFencingToken != lease.FencingToken || state.ExpiresAt is not DateTimeOffset expiry || expiry <= now)
                return (state, (ReconciliationLease?)null);
            ReconciliationLease renewed = lease with { ExpiresAt = now.Add(ttl) };
            return (state with { ExpiresAt = renewed.ExpiresAt }, renewed);
        }).ConfigureAwait(false);
    }

    public async ValueTask ReleaseAsync(ReconciliationLease lease, CancellationToken cancellationToken = default)
    {
        _ = await WithExclusiveAsync(lease.ResourceIdentity, cancellationToken, state =>
        {
            if (state.OwnerId == lease.OwnerId && state.LastFencingToken == lease.FencingToken)
                state = state with { OwnerId = null, ExpiresAt = null };
            return (state, true);
        }).ConfigureAwait(false);
    }

    private async ValueTask<T> WithExclusiveAsync<T>(
        string resourceIdentity,
        CancellationToken cancellationToken,
        Func<LeaseFileState, (LeaseFileState State, T Result)> operation)
    {
        string path = PathFor(resourceIdentity);
        policy.EnsureFileAccess(path);
        Directory.CreateDirectory(directory);
        SemaphoreSlim local = localGates.GetOrAdd(resourceIdentity, _ => new SemaphoreSlim(1, 1));
        await local.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            FileStream? stream = null;
            try
            {
                stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.WriteThrough);
            }
            catch (IOException)
            {
                return default!;
            }
            await using (stream)
            {
                LeaseFileState state = await ReadAsync(stream, cancellationToken).ConfigureAwait(false);
                (LeaseFileState next, T result) = operation(state);
                await WriteAsync(stream, next, cancellationToken).ConfigureAwait(false);
                return result;
            }
        }
        finally { local.Release(); }
    }

    private static async ValueTask<LeaseFileState> ReadAsync(FileStream stream, CancellationToken cancellationToken)
    {
        if (stream.Length == 0) return new(0, null, null);
        stream.Position = 0;
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
        string json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<LeaseFileState>(json) ?? new(0, null, null);
    }

    private static async ValueTask WriteAsync(FileStream stream, LeaseFileState state, CancellationToken cancellationToken)
    {
        stream.Position = 0;
        stream.SetLength(0);
        await using StreamWriter writer = new(stream, new UTF8Encoding(false), 1024, leaveOpen: true);
        await writer.WriteAsync(JsonSerializer.Serialize(state).AsMemory(), cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        stream.Flush(flushToDisk: true);
    }

    private string PathFor(string identity)
    {
        string name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
        return Path.Combine(directory, name + ".lease.json");
    }

    private static void Validate(string identity, string owner, TimeSpan ttl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        if (ttl <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ttl));
    }

    private sealed record LeaseFileState(long LastFencingToken, string? OwnerId, DateTimeOffset? ExpiresAt);
}

public interface IReconciliationLeaseContextAccessor
{
    ReconciliationLease? Current { get; }
    IDisposable Push(ReconciliationLease lease);
}

public sealed class ReconciliationLeaseContextAccessor : IReconciliationLeaseContextAccessor
{
    private readonly AsyncLocal<ReconciliationLease?> current = new();
    public ReconciliationLease? Current => current.Value;

    public IDisposable Push(ReconciliationLease lease)
    {
        ReconciliationLease? previous = current.Value;
        current.Value = lease;
        return new Scope(() => current.Value = previous);
    }

    private sealed class Scope(Action dispose) : IDisposable
    {
        private Action? action = dispose;
        public void Dispose() => Interlocked.Exchange(ref action, null)?.Invoke();
    }
}

public sealed record ReconciliationCoordinationOptions(TimeSpan LeaseTtl)
{
    public static ReconciliationCoordinationOptions Default { get; } = new(TimeSpan.FromSeconds(30));
}

public sealed class ReconciliationLeaseUnavailableException(string resourceIdentity)
    : InvalidOperationException($"Reconciliation target '{resourceIdentity}' is already leased by another run.");

public sealed class ReconciliationLeaseLostException(string resourceIdentity, long fencingToken)
    : InvalidOperationException($"Reconciliation lease for '{resourceIdentity}' with fencing token {fencingToken} was lost.");

public sealed class ReconciliationCoordinator(
    IReconciliationExecutor inner,
    IReconciliationLeaseStore leases,
    IReconciliationLeaseContextAccessor accessor,
    ReconciliationCoordinationOptions options) : IReconciliationExecutor
{
    public async ValueTask<ReconciliationRunResult> RunAsync(
        SyncDefinition definition,
        ResourceStateSnapshot? baseline = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        TimeSpan ttl = options.LeaseTtl;
        if (ttl <= TimeSpan.Zero) throw new InvalidOperationException("Reconciliation lease TTL must be positive.");
        string identity = ResourceIdentity.Parse(definition.Goal.TargetResource).ToString();
        string owner = $"{Environment.ProcessId}:{Guid.NewGuid():N}";
        ReconciliationLease? lease = await leases.TryAcquireAsync(identity, owner, ttl, cancellationToken).ConfigureAwait(false);
        if (lease is null)
            return Failure(definition, new ReconciliationLeaseUnavailableException(identity));

        using IDisposable scope = accessor.Push(lease);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task<ReconciliationRunResult> run = inner.RunAsync(definition, baseline, linked.Token).AsTask();
        Task heartbeat = HeartbeatAsync(lease, ttl, linked.Token);
        try
        {
            Task completed = await Task.WhenAny(run, heartbeat).ConfigureAwait(false);
            if (completed == heartbeat)
            {
                if (heartbeat.IsFaulted)
                {
                    linked.Cancel();
                    try { await run.ConfigureAwait(false); } catch { }
                    Exception error = heartbeat.Exception?.GetBaseException() ?? new ReconciliationLeaseLostException(identity, lease.FencingToken);
                    return Failure(definition, error);
                }
            }
            return await run.ConfigureAwait(false);
        }
        finally
        {
            linked.Cancel();
            try { await heartbeat.ConfigureAwait(false); } catch (OperationCanceledException) { } catch { }
            try { await leases.ReleaseAsync(lease, CancellationToken.None).ConfigureAwait(false); } catch { }
        }
    }

    private async Task HeartbeatAsync(ReconciliationLease lease, TimeSpan ttl, CancellationToken cancellationToken)
    {
        TimeSpan interval = TimeSpan.FromTicks(Math.Max(TimeSpan.FromMilliseconds(100).Ticks, ttl.Ticks / 3));
        ReconciliationLease current = lease;
        while (true)
        {
            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            current = await leases.RenewAsync(current, ttl, cancellationToken).ConfigureAwait(false)
                ?? throw new ReconciliationLeaseLostException(lease.ResourceIdentity, lease.FencingToken);
        }
    }

    private static ReconciliationRunResult Failure(SyncDefinition definition, Exception error) =>
        new(definition, null, null, null, null, Array.Empty<ExecutionStepResult>(), false, error);
}

public static class ReconciliationCoordinationServiceCollectionExtensions
{
    public static IServiceCollection AddDurableFluNetReconciliationCoordination(
        this IServiceCollection services,
        string directory,
        TimeSpan? leaseTtl = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(new DurableReconciliationLeaseOptions(directory));
        services.AddSingleton<IReconciliationLeaseStore, DurableReconciliationLeaseStore>();
        services.AddSingleton<IReconciliationLeaseContextAccessor, ReconciliationLeaseContextAccessor>();
        services.AddSingleton(leaseTtl is TimeSpan ttl ? new ReconciliationCoordinationOptions(ttl) : ReconciliationCoordinationOptions.Default);
        return services;
    }
}
