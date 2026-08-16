using FluNET.Prompt;
using FluNET.Prompt.Surface;

namespace FluNET.Language.Resources;

public sealed class ConfigurationResourceProvider : IResourceProvider
{
    public string Id => "core.configuration";
    public IReadOnlyList<ResourceCapability> RequiredCapabilities => [ResourceCapability.EnvironmentRead];
    public bool CanHandle(ResourceDescriptor descriptor) => descriptor.Reference is ConfigurationResourceReference;

    public ResourceProviderResult LowerRead(ResourceProviderContext context)
    {
        if (context.Intent != ResourceReadIntent.Get) return ResourceProviderResult.Error("FLN361", "Configuration belongs to GET.");
        ConfigurationResourceReference reference = (ConfigurationResourceReference)context.Descriptor.Reference;
        return new([new CommandSyntax([
            new("GETCONFIG", PromptTokenKind.Word, context.SurfaceCommand.Span.Start, 0),
            new($"[{context.OutputVariable}]", PromptTokenKind.Variable, context.Source.Span.Start, 0),
            new("FROM", PromptTokenKind.Word, context.Source.Span.Start, 0),
            new($"{{{reference.Key}}}", PromptTokenKind.Reference, context.Source.Span.Start, 0)
        ], context.Grammar)]);
    }
}
