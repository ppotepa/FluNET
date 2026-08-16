using FluNET.Capabilities;

namespace FluNET.Automation;

/// <summary>Provider-neutral event envelope delivered to WATCH automations.</summary>
public sealed record AutomationSignal(
    string Resource,
    string? EventName,
    IReadOnlyDictionary<string, object?> Data)
{
    public static AutomationSignal FromFileChange(string resource, FluNetFileChange change) =>
        new(resource, change.Kind.ToString().ToUpperInvariant(), new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["kind"] = change.Kind.ToString().ToUpperInvariant(),
            ["path"] = change.Path,
            ["oldPath"] = change.OldPath,
            ["timestamp"] = change.Timestamp,
            ["isDirectory"] = change.IsDirectory,
            ["length"] = change.Length
        });
}
