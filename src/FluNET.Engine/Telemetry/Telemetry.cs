using FluNET.Declarative.Reconciliation;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace FluNET.Telemetry;

public sealed record FluNetTelemetryEvent(
    DateTimeOffset Timestamp,
    string Category,
    string Name,
    string Outcome,
    TimeSpan Duration,
    IReadOnlyDictionary<string, string> Attributes);

public interface IFluNetTelemetrySink
{
    ValueTask EmitAsync(FluNetTelemetryEvent item, CancellationToken cancellationToken = default);
}

public sealed class NullFluNetTelemetrySink : IFluNetTelemetrySink
{
    public static NullFluNetTelemetrySink Instance { get; } = new();
    private NullFluNetTelemetrySink() { }
    public ValueTask EmitAsync(FluNetTelemetryEvent item, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.CompletedTask; }
}

public sealed class InMemoryFluNetTelemetrySink : IFluNetTelemetrySink
{
    private readonly ConcurrentQueue<FluNetTelemetryEvent> items = new();
    public IReadOnlyList<FluNetTelemetryEvent> Snapshot() => items.ToArray();
    public ValueTask EmitAsync(FluNetTelemetryEvent item, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); items.Enqueue(item); return ValueTask.CompletedTask; }
}

internal static class FluNetTelemetry
{
    public static async ValueTask TryEmitAsync(IFluNetTelemetrySink sink, FluNetTelemetryEvent item)
    {
        try { await sink.EmitAsync(item, CancellationToken.None).ConfigureAwait(false); }
        catch { /* telemetry must never change program outcome */ }
    }
}

/// <summary>Metadata-only reconciliation telemetry decorator. It never emits record values or resource paths.</summary>
public sealed class TelemetryReconciliationExecutor(
    IReconciliationExecutor inner,
    IFluNetTelemetrySink telemetry,
    IReconciliationLeaseContextAccessor leases) : IReconciliationExecutor
{
    public async ValueTask<ReconciliationRunResult> RunAsync(
        SyncDefinition definition,
        ResourceStateSnapshot? baseline = null,
        CancellationToken cancellationToken = default)
    {
        long started = Stopwatch.GetTimestamp();
        ReconciliationRunResult result = await inner.RunAsync(definition, baseline, cancellationToken).ConfigureAwait(false);
        ReconciliationDiff? diff = result.Diff;
        Dictionary<string, string> attributes = new(StringComparer.Ordinal)
        {
            ["definition.id"] = definition.Id,
            ["target.kind"] = definition.TargetDescriptor.Reference.Kind.ToString(),
            ["source.kind"] = definition.SourceDescriptor.Reference.Kind.ToString(),
            ["conflict.policy"] = definition.Goal.ConflictPolicy.ToString(),
            ["applied"] = result.Applied.ToString(),
            ["creates"] = (diff?.Creates ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["updates"] = (diff?.Updates ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["deletes"] = (diff?.Deletes ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["conflicts"] = (diff?.Conflicts ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        if (result.Mutation?.MutatorId is string mutator) attributes["mutator.id"] = mutator;
        if (leases.Current is ReconciliationLease lease) attributes["fencing.token"] = lease.FencingToken.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string outcome = result.IsSuccess ? result.Applied ? "applied" : "converged" : "failed";
        await FluNetTelemetry.TryEmitAsync(telemetry, new(
            DateTimeOffset.UtcNow,
            "reconciliation",
            "sync",
            outcome,
            Stopwatch.GetElapsedTime(started),
            attributes)).ConfigureAwait(false);
        return result;
    }
}
