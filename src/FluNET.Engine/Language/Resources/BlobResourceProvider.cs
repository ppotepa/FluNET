using FluNET.Capabilities;
using FluNET.Prompt;
using FluNET.Prompt.Surface;

namespace FluNET.Language.Resources;

public sealed class BlobResourceProvider : IResourceProvider
{
    public BlobResourceProvider(IFluNetBlobStore store) =>
        ArgumentNullException.ThrowIfNull(store);

    public string Id => "storage.blob";

    public bool CanHandle(ResourceDescriptor descriptor) =>
        descriptor.Reference is ModuleResourceReference module &&
        module.Scheme.Equals("blob", StringComparison.OrdinalIgnoreCase);

    public ResourceProviderResult LowerRead(ResourceProviderContext context)
    {
        if (context.Intent != ResourceReadIntent.Get)
            return ResourceProviderResult.Error("FLN341", "Blob resources belong to GET.");

        ModuleResourceReference module = (ModuleResourceReference)context.Descriptor.Reference;
        return new([
            new CommandSyntax([
                new("GETBLOB", PromptTokenKind.Word, context.SurfaceCommand.Span.Start, 0),
                new($"[{context.OutputVariable}]", PromptTokenKind.Variable, context.Source.Span.Start, 0),
                new("FROM", PromptTokenKind.Word, context.Source.Span.Start, 0),
                new($"{{{module.Value}}}", PromptTokenKind.Reference, context.Source.Span.Start, 0)
            ], context.Grammar)
        ]);
    }
}
