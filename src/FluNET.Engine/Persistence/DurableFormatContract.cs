namespace FluNET.Persistence;

public sealed record DurableFormatContract(
    string Id,
    int Version,
    string FilePattern,
    string WriteModel,
    string Integrity,
    string Scope);

/// <summary>
/// Candidate compatibility contract for built-in durable formats. A version change is required
/// before an incompatible reader/writer change may ship after 1.0.
/// </summary>
public sealed record DurableFormatContractManifest(
    string ContractVersion,
    IReadOnlyList<DurableFormatContract> Formats)
{
    public static DurableFormatContractManifest Candidate1_0 { get; } = new(
        "1.0-rc-candidate",
        [
            new("workflow-journal", 1, "*.journal.jsonl", "append-only JSONL envelope", "SHA-256 checksum per event", "single-host"),
            new("reconciliation-baseline", 1, "*.reconciliation.json", "atomic JSON envelope replace", "SHA-256 payload checksum", "single-host"),
            new("reconciliation-checkpoint", 1, "*.checkpoint.jsonl", "append-only JSONL envelope", "SHA-256 checksum per checkpoint", "single-host"),
            new("reconciliation-lease", 1, "*.lease.json", "exclusive shared-file snapshot", "exclusive ownership + monotonic fencing token", "shared-filesystem"),
            new("automation-schedule", 1, "host-selected JSON file", "atomic snapshot replace", "host filesystem policy", "single-host"),
            new("execution-cache", 1, "host-selected JSON file", "atomic snapshot replace", "runtime type envelope", "single-host"),
            new("idempotency", 1, "host-selected JSON file", "atomic snapshot replace", "runtime type envelope", "single-host")
        ]);
}
