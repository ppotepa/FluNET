namespace FluNET.Compatibility;

public enum CompatibilityTier
{
    Preferred,
    LegacySupported,
    InternalOnly
}

public sealed record CompatibilityContractEntry(
    string Surface,
    CompatibilityTier Tier,
    string Replacement,
    string Policy);

/// <summary>
/// 1.0-candidate compatibility ledger. It centralizes migration intent without
/// removing legacy APIs before the verified compatibility release.
/// </summary>
public static class CompatibilityContract
{
    public const string ContractVersion = "1.0-candidate";

    public static IReadOnlyList<CompatibilityContractEntry> Entries { get; } =
    [
        new("FluNET.Language + IFluNetModule", CompatibilityTier.Preferred,
            "stable module declarations", "new language/runtime extensions belong here"),
        new("FluNET.Execution.Commands typed command APIs", CompatibilityTier.Preferred,
            "ICommand/ICommandBinder/ICommandHandler", "canonical execution extension point"),
        new("FluNET.Compilation.SurfaceCompiler", CompatibilityTier.Preferred,
            "compact compiler/lowering pipeline", "preferred compact-language front end"),
        new("FluNET.Sentences", CompatibilityTier.LegacySupported,
            "typed command + semantic frame APIs", "retained for source compatibility; do not use for new modules"),
        new("FluNET.Words / legacy verb objects", CompatibilityTier.LegacySupported,
            "IFluNetModule declarations", "compatibility projection only"),
        new("FluNET.Tokens.Tree", CompatibilityTier.LegacySupported,
            "PromptSyntax / SurfaceProgramSyntax", "legacy parse representation"),
        new("FluNET.Compatibility.LegacySentenceAdapter", CompatibilityTier.LegacySupported,
            "typed compiler routes", "bridge only; not a new execution path"),
        new("implementation/private helper namespaces", CompatibilityTier.InternalOnly,
            "public contract manifests", "not part of compatibility guarantee")
    ];

    public static bool IsPreferred(string surface) => Entries.Any(entry =>
        entry.Surface.Equals(surface, StringComparison.OrdinalIgnoreCase) &&
        entry.Tier == CompatibilityTier.Preferred);
}
