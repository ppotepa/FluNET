namespace FluNET.Language.Contracts;

public enum ReleaseGateCheckId
{
    Restore,
    ReleaseBuild,
    ReleaseTests,
    StaticContracts,
    ToolDirectSmoke,
    ToolPack,
    ToolInstallSmoke
}

public sealed record ReleaseGateCheckResult(
    ReleaseGateCheckId Check,
    bool Passed,
    string? Evidence = null);

public sealed record ReleaseGateAssessment(
    string CandidateVersion,
    string PublicLanguageVersion,
    IReadOnlyList<ReleaseGateCheckResult> Checks,
    IReadOnlyList<string> Blockers)
{
    public bool HasCompleteEvidence => Blockers.Count == 0;
    public bool ReadyForVersionPromotion => HasCompleteEvidence && PublicLanguageVersion != CandidateVersion;
    public bool ReadyForRelease => HasCompleteEvidence && PublicLanguageVersion == CandidateVersion;
}

/// <summary>
/// Pure policy for interpreting externally produced Release evidence. It never executes build/test
/// commands and therefore cannot convert static source inspection into a passing Release gate.
/// </summary>
public static class ReleasePromotionGate
{
    private static readonly ReleaseGateCheckId[] Required =
    [
        ReleaseGateCheckId.Restore,
        ReleaseGateCheckId.ReleaseBuild,
        ReleaseGateCheckId.ReleaseTests,
        ReleaseGateCheckId.StaticContracts,
        ReleaseGateCheckId.ToolDirectSmoke,
        ReleaseGateCheckId.ToolPack,
        ReleaseGateCheckId.ToolInstallSmoke
    ];

    public static ReleaseGateAssessment Assess1_0(
        IEnumerable<ReleaseGateCheckResult> evidence,
        string? publicLanguageVersion = null)
    {
        ReleaseGateCheckResult[] snapshot = (evidence ?? throw new ArgumentNullException(nameof(evidence))).ToArray();
        string[] blockers = Required
            .Select(required =>
            {
                ReleaseGateCheckResult[] matches = snapshot.Where(item => item.Check == required).ToArray();
                if (matches.Length == 0) return $"Missing evidence: {required}.";
                if (matches.Length > 1) return $"Duplicate evidence: {required}.";
                return matches[0].Passed ? null : $"Failed gate: {required}{(string.IsNullOrWhiteSpace(matches[0].Evidence) ? "." : $" — {matches[0].Evidence}")}";
            })
            .Where(message => message is not null)
            .Cast<string>()
            .ToArray();

        return new(
            "1.0",
            publicLanguageVersion ?? StandardLanguageIdentity.Version.Value,
            snapshot,
            blockers);
    }
}
