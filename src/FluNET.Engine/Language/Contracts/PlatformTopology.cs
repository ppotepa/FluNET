namespace FluNET.Language.Contracts;

public enum PlatformModuleKind
{
    Core,
    CompactSurface,
    Data,
    Automation,
    Reconciliation,
    Provider
}

public sealed record PlatformModuleBoundary(
    string Id,
    PlatformModuleKind Kind,
    string PhysicalAssembly,
    IReadOnlyList<string> DependsOn,
    string Responsibility);

/// <summary>
/// Logical 1.0 module topology. Physical package splitting is deliberately
/// independent so the verified contract is not tied to a premature csproj layout.
/// </summary>
public static class FluNetPlatformTopology
{
    public static IReadOnlyList<PlatformModuleBoundary> Modules { get; } =
    [
        new("flunet.core", PlatformModuleKind.Core, "FluNET.Engine", [],
            "canonical grammar, stable identities, structural types, typed command runtime"),
        new("flunet.surface", PlatformModuleKind.CompactSurface, "FluNET.Engine", ["flunet.core"],
            "compact AST, inference, lowering, tooling and resource-oriented authoring"),
        new("flunet.data", PlatformModuleKind.Data, "FluNET.Engine", ["flunet.core", "flunet.surface"],
            "typed collection transforms, projections, joins and nested iteration actions"),
        new("flunet.automation", PlatformModuleKind.Automation, "FluNET.Engine", ["flunet.core", "flunet.surface"],
            "trigger compilation, host-driven scheduling, durable schedule state"),
        new("flunet.reconciliation", PlatformModuleKind.Reconciliation, "FluNET.Engine", ["flunet.core", "flunet.surface", "flunet.data", "flunet.automation"],
            "desired/observed state, SYNC, diff, compensation, saga and history orchestration"),
        new("flunet.providers", PlatformModuleKind.Provider, "FluNET.Engine", ["flunet.core"],
            "file/HTTP/environment/secret/SQL acquisition, decoding, authentication and observation boundaries")
    ];

    public static PlatformModuleBoundary Get(string id) => Modules.Single(module =>
        module.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public static bool IsAllowedDependency(string from, string to) =>
        Get(from).DependsOn.Contains(to, StringComparer.OrdinalIgnoreCase);
}
