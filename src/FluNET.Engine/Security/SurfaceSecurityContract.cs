using FluNET.Compilation;
using FluNET.Compilation.Inference;
using FluNET.Language;
using FluNET.Language.Resources;
using FluNET.Prompt;
using FluNET.Prompt.Surface;

namespace FluNET.Security;

public enum FluNetCapability
{
    FileRead,
    FileWrite,
    NetworkRead,
    NetworkWrite,
    EnvironmentRead,
    SecretRead,
    DatabaseRead,
    TextOutput,
    EmailSend
}

public sealed record CapabilityRequirement(
    FluNetCapability Capability,
    int CommandIndex,
    string FrameId,
    string Reason);

public sealed record SurfaceSecurityManifest(
    IReadOnlyList<CapabilityRequirement> Requirements)
{
    public IReadOnlyList<FluNetCapability> RequiredCapabilities => Requirements
        .Select(item => item.Capability)
        .Distinct()
        .OrderBy(item => item)
        .ToArray();

    public bool Requires(FluNetCapability capability) =>
        Requirements.Any(item => item.Capability == capability);
}

/// <summary>Side-effect-free capability projection over a compiled surface program.</summary>
public sealed class SurfaceSecurityAnalyzer
{
    public SurfaceSecurityManifest Analyze(SurfaceCompilationResult compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        if (compilation.BoundProgram is null)
            return new(Array.Empty<CapabilityRequirement>());

        List<CapabilityRequirement> requirements = [];
        for (int index = 0; index < compilation.BoundProgram.Commands.Count; index++)
        {
            var command = compilation.BoundProgram.Commands[index];
            string frame = command.Frame.Id.Value;
            foreach ((FluNetCapability capability, string reason) in FrameRequirements(command.Frame.Id))
                Add(requirements, capability, index, frame, reason);

            if (frame.StartsWith("surface.get.http.", StringComparison.OrdinalIgnoreCase) &&
                command.Syntax.AllTokens.Any(token => token.Text.Equals("USING", StringComparison.OrdinalIgnoreCase)))
                Add(requirements, FluNetCapability.SecretRead, index, frame, "authenticated HTTP credential");

            if (frame.Equals("surface.flow.foreach.json", StringComparison.OrdinalIgnoreCase))
                AnalyzeNestedActions(command.Syntax.AllTokens, index, frame, requirements);
        }
        return new(requirements
            .DistinctBy(item => (item.Capability, item.CommandIndex, item.FrameId, item.Reason))
            .OrderBy(item => item.CommandIndex)
            .ThenBy(item => item.Capability)
            .ToArray());
    }

    private static IEnumerable<(FluNetCapability Capability, string Reason)> FrameRequirements(FrameId frameId)
    {
        string frame = frameId.Value;
        if (frame is "core.get.text" or "core.load.text" or "core.load.config" || frame.StartsWith("surface.load.", StringComparison.OrdinalIgnoreCase))
            yield return (FluNetCapability.FileRead, "local resource read");
        if (frame.StartsWith("surface.get.http.", StringComparison.OrdinalIgnoreCase))
            yield return (FluNetCapability.NetworkRead, "HTTP resource read");
        if (frame == "surface.get.environment")
            yield return (FluNetCapability.EnvironmentRead, "environment resource read");
        if (frame == "surface.get.secret")
            yield return (FluNetCapability.SecretRead, "secret resource read");
        if (frame == "surface.get.sql")
            yield return (FluNetCapability.DatabaseRead, "SQL resource read");
        if (frame is "core.save.text" or "core.delete.file")
            yield return (FluNetCapability.FileWrite, "local file mutation");
        if (frame == "core.download.file")
        {
            yield return (FluNetCapability.NetworkRead, "download source");
            yield return (FluNetCapability.FileWrite, "download target");
        }
        if (frame == "core.post.json")
            yield return (FluNetCapability.NetworkWrite, "HTTP mutation");
        if (frame == "core.send.email")
            yield return (FluNetCapability.EmailSend, "email mutation");
        if (frame == "core.say.text")
            yield return (FluNetCapability.TextOutput, "text output");
    }

    private static void AnalyzeNestedActions(
        IEnumerable<PromptToken> tokens,
        int commandIndex,
        string frame,
        ICollection<CapabilityRequirement> requirements)
    {
        PromptToken? encoded = tokens.LastOrDefault(token => token.Kind == PromptTokenKind.Reference);
        if (encoded is null) return;
        string value = encoded.Text;
        if (value.Length >= 2 && value[0] == '{' && value[^1] == '}') value = value[1..^1];
        SurfaceForEachDescriptor descriptor;
        try { descriptor = SurfaceForEachDescriptor.Decode(value); }
        catch (FormatException) { return; }

        foreach (SurfaceIterationActionDescriptor action in descriptor.Actions)
        {
            switch (action.Kind.ToUpperInvariant())
            {
                case "SAY":
                    Add(requirements, FluNetCapability.TextOutput, commandIndex, frame, "nested SAY action");
                    break;
                case "LOAD":
                    Add(requirements, FluNetCapability.FileRead, commandIndex, frame, "nested LOAD action");
                    break;
                case "SAVE":
                    Add(requirements, FluNetCapability.FileWrite, commandIndex, frame, "nested SAVE action");
                    break;
                case "POST":
                    Add(requirements, FluNetCapability.NetworkWrite, commandIndex, frame, "nested POST action");
                    break;
                case "GET":
                    AnalyzeNestedGet(action.Source, commandIndex, frame, requirements);
                    break;
            }
        }
    }

    private static void AnalyzeNestedGet(
        string source,
        int commandIndex,
        string frame,
        ICollection<CapabilityRequirement> requirements)
    {
        try
        {
            ResourceReference reference = new ResourceClassifier().Classify(new SurfaceValueSyntax(source, default));
            FluNetCapability capability = reference.Kind switch
            {
                ResourceKind.LocalFile => FluNetCapability.FileRead,
                ResourceKind.Http => FluNetCapability.NetworkRead,
                ResourceKind.Environment => FluNetCapability.EnvironmentRead,
                ResourceKind.Secret => FluNetCapability.SecretRead,
                ResourceKind.Sql => FluNetCapability.DatabaseRead,
                _ => FluNetCapability.NetworkRead
            };
            Add(requirements, capability, commandIndex, frame, $"nested GET {reference.Kind} resource");
        }
        catch (FormatException)
        {
            Add(requirements, FluNetCapability.NetworkRead, commandIndex, frame, "dynamic nested GET resource requires host review");
        }
    }

    private static void Add(
        ICollection<CapabilityRequirement> requirements,
        FluNetCapability capability,
        int commandIndex,
        string frame,
        string reason) =>
        requirements.Add(new(capability, commandIndex, frame, reason));
}
