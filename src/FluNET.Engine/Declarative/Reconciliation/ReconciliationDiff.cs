namespace FluNET.Declarative.Reconciliation;

public enum ReconciliationChangeKind
{
    Create,
    Update,
    Delete,
    Unchanged,
    Conflict
}

public sealed record ReconciliationChange(
    string Key,
    ReconciliationChangeKind Kind,
    StateRecord? Desired,
    StateRecord? Observed,
    StateRecord? Baseline = null);

public sealed record ReconciliationDiff(IReadOnlyList<ReconciliationChange> Changes)
{
    public int Creates => Changes.Count(change => change.Kind == ReconciliationChangeKind.Create);
    public int Updates => Changes.Count(change => change.Kind == ReconciliationChangeKind.Update);
    public int Deletes => Changes.Count(change => change.Kind == ReconciliationChangeKind.Delete);
    public int Unchanged => Changes.Count(change => change.Kind == ReconciliationChangeKind.Unchanged);
    public int Conflicts => Changes.Count(change => change.Kind == ReconciliationChangeKind.Conflict);
    public bool HasChanges => Creates + Updates + Deletes + Conflicts > 0;
    public bool HasMutations => Creates + Updates + Deletes > 0;
    public bool HasConflicts => Conflicts > 0;
}

/// <summary>
/// Keyed desired/observed diff. Desired is authoritative. An optional baseline enables
/// true three-way conflict detection without treating ordinary target drift as a conflict.
/// </summary>
public sealed class ReconciliationDiffEngine
{
    public ReconciliationDiff Compare(
        DesiredStateSnapshot desired,
        ObservedStateSnapshot observed,
        ResourceStateSnapshot? baseline = null)
    {
        ArgumentNullException.ThrowIfNull(desired);
        ArgumentNullException.ThrowIfNull(observed);
        if (!desired.KeyField.Equals(observed.KeyField, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Desired and observed snapshots must use the same key field.");
        if (baseline is not null && !desired.KeyField.Equals(baseline.KeyField, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Baseline snapshot must use the same key field as desired state.", nameof(baseline));

        Dictionary<string, StateRecord> wanted = desired.Records.ToDictionary(item => item.Key, StringComparer.Ordinal);
        Dictionary<string, StateRecord> current = observed.Records.ToDictionary(item => item.Key, StringComparer.Ordinal);
        Dictionary<string, StateRecord>? previous = baseline?.Records.ToDictionary(item => item.Key, StringComparer.Ordinal);
        string[] keys = wanted.Keys
            .Concat(current.Keys)
            .Concat(previous?.Keys ?? Array.Empty<string>())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        ReconciliationChange[] changes = keys
            .Select(key => previous is null
                ? TwoWay(key, wanted.GetValueOrDefault(key), current.GetValueOrDefault(key))
                : ThreeWay(
                    key,
                    wanted.GetValueOrDefault(key),
                    current.GetValueOrDefault(key),
                    previous.GetValueOrDefault(key)))
            .ToArray();
        return new(changes);
    }

    private static ReconciliationChange TwoWay(
        string key,
        StateRecord? desired,
        StateRecord? observed)
    {
        if (desired is null && observed is null)
            throw new InvalidOperationException("A diff key must exist on at least one side.");
        if (desired is null) return new(key, ReconciliationChangeKind.Delete, null, observed);
        if (observed is null) return new(key, ReconciliationChangeKind.Create, desired, null);
        return Same(desired, observed)
            ? new(key, ReconciliationChangeKind.Unchanged, desired, observed)
            : new(key, ReconciliationChangeKind.Update, desired, observed);
    }

    private static ReconciliationChange ThreeWay(
        string key,
        StateRecord? desired,
        StateRecord? observed,
        StateRecord? baseline)
    {
        if (desired is not null && observed is not null && Same(desired, observed))
            return new(key, ReconciliationChangeKind.Unchanged, desired, observed, baseline);

        if (baseline is null)
        {
            if (desired is null) return new(key, ReconciliationChangeKind.Delete, null, observed, null);
            if (observed is null) return new(key, ReconciliationChangeKind.Create, desired, null, null);
            return new(key, ReconciliationChangeKind.Conflict, desired, observed, null);
        }

        bool desiredChanged = !SameOptional(desired, baseline);
        bool observedChanged = !SameOptional(observed, baseline);
        if (desiredChanged && observedChanged)
            return new(key, ReconciliationChangeKind.Conflict, desired, observed, baseline);
        if (!desiredChanged && !observedChanged)
            return new(key, ReconciliationChangeKind.Unchanged, desired, observed, baseline);

        // Desired is authoritative whenever only one side moved away from the baseline.
        if (desired is null)
            return new(key, ReconciliationChangeKind.Delete, null, observed, baseline);
        if (observed is null)
            return new(key, ReconciliationChangeKind.Create, desired, null, baseline);
        return new(key, ReconciliationChangeKind.Update, desired, observed, baseline);
    }

    private static bool SameOptional(StateRecord? left, StateRecord? right) =>
        left is null && right is null ||
        left is not null && right is not null && Same(left, right);

    private static bool Same(StateRecord left, StateRecord right) =>
        left.Fingerprint.Equals(right.Fingerprint, StringComparison.Ordinal);
}
