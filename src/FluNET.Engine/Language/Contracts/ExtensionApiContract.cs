using FluNET.Capabilities;
using FluNET.Declarative.Reconciliation;
using FluNET.Execution.Commands;
using FluNET.Language.Resources;
using FluNET.Language.Values;
using FluNET.Telemetry;

namespace FluNET.Language.Contracts;

public sealed record ExtensionContractEntry(
    string Category,
    string ContractName,
    string Stability,
    string Purpose);

public sealed record ExtensionApiContractManifest(
    string ContractVersion,
    IReadOnlyList<ExtensionContractEntry> Entries)
{
    public static ExtensionApiContractManifest Candidate1_0 { get; } = new(
        "1.0-rc-candidate",
        EntriesForCandidate());

    private static IReadOnlyList<ExtensionContractEntry> EntriesForCandidate()
    {
        static string Name(Type type) => type.FullName ?? type.Name;
        return new ExtensionContractEntry[]
        {
            new("module", Name(typeof(IFluNetModule)), "stable-candidate", "register language declarations and runtime routes"),
            new("module", Name(typeof(FluNetModuleBuilder)), "stable-candidate", "compose types, commands, codecs and resource extensions"),
            new("command", Name(typeof(ICommand<>)), "stable-candidate", "typed command value contract"),
            new("command", Name(typeof(ICommandBinder<,>)), "stable-candidate", "compile a bound frame into a typed command"),
            new("command", Name(typeof(ICommandHandler<,>)), "stable-candidate", "execute a typed command through host capabilities"),
            new("value", Name(typeof(IValueCodec<>)), "stable-candidate", "language value parse/format boundary"),
            new("value", Name(typeof(IValueConversion<,>)), "stable-candidate", "explicit typed conversion edge"),
            new("resource", Name(typeof(IResourceProvider)), "stable-candidate", "lower a resource descriptor into canonical read operations"),
            new("resource", Name(typeof(IResourceDecoder)), "stable-candidate", "decode resource payload bytes into a typed language value"),
            new("resource", Name(typeof(IResourceEncoder)), "stable-candidate", "encode a typed language value into a resource payload"),
            new("reconciliation", Name(typeof(IResourceObserver)), "stable-candidate", "observe keyed current state without mutation"),
            new("reconciliation", Name(typeof(IReconciliationMutator)), "stable-candidate", "produce an ordinary mutation execution plan"),
            new("reconciliation", Name(typeof(IReconciliationStateStore)), "stable-candidate", "persist last converged baseline state"),
            new("reconciliation", Name(typeof(IReconciliationLeaseStore)), "stable-candidate", "coordinate target ownership with fencing tokens"),
            new("reconciliation", Name(typeof(IReconciliationCheckpointStore)), "stable-candidate", "persist reconciliation phase checkpoints"),
            new("reconciliation", Name(typeof(IReconciliationLeaseContextAccessor)), "stable-candidate", "expose current fencing token to mutation providers"),
            new("capability", Name(typeof(IExecutionPolicy)), "stable-candidate", "host file/network authorization boundary"),
            new("capability", Name(typeof(ISecretStore)), "stable-candidate", "opaque secret lookup"),
            new("capability", Name(typeof(ISecretAccessPolicy)), "stable-candidate", "host authorization for secret names"),
            new("capability", Name(typeof(IHttpTransport)), "stable-candidate", "ordinary HTTP transport boundary"),
            new("capability", Name(typeof(IAuthenticatedHttpTransport)), "stable-candidate", "authenticated HTTP transport boundary"),
            new("capability", Name(typeof(IHttpAuthenticationScheme)), "stable-candidate", "apply an opaque credential to an HTTP request"),
            new("capability", Name(typeof(ISqlQueryExecutor)), "stable-candidate", "provider-neutral database query boundary"),
            new("telemetry", Name(typeof(IFluNetTelemetrySink)), "stable-candidate", "metadata-only execution telemetry sink")
        };
    }

    public bool Contains(Type contractType) => Entries.Any(entry =>
        entry.ContractName.Equals(contractType.FullName ?? contractType.Name, StringComparison.Ordinal));
}
