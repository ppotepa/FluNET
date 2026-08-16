using FluNET.Diagnostics;

namespace FluNET.Language;

public sealed record LanguageBuildResult(
    LanguageSnapshot Snapshot,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool Success => Diagnostics.All(x => x.Severity != DiagnosticSeverity.Error);
}
