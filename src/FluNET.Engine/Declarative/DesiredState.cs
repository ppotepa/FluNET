using FluNET.Automation;
using FluNET.Compilation;
using FluNET.Prompt;

namespace FluNET.Declarative;

public sealed record EnsureGoal(
    string Target,
    string Source,
    TimeSpan? RefreshInterval,
    int? KeepVersions,
    bool NotifyOnFailure,
    SourceSpan Span);

public sealed record DesiredStatePlan(
    EnsureGoal Goal,
    SurfaceCompilationResult Compilation,
    AutomationDefinition? RefreshAutomation)
{
    public bool IsValid => Compilation.IsValid;
}

public sealed record DesiredStateDiagnostic(string Code, string Message, SourceSpan Span);

public sealed record DesiredStateCompilationResult(
    IReadOnlyList<DesiredStatePlan> Plans,
    IReadOnlyList<DesiredStateDiagnostic> Diagnostics)
{
    public bool IsValid => Diagnostics.Count == 0 && Plans.All(plan => plan.IsValid);
}
