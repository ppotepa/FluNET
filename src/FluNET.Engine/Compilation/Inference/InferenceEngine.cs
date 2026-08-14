using FluNET.Language;
using FluNET.Language.Resources;
using FluNET.Prompt.Surface;

namespace FluNET.Compilation.Inference;

public sealed class InferenceEngine(ResourceClassifier resources, FormatInference formats, VariableNameInference names)
{
    public InferenceEngine() : this(new ResourceClassifier(), new FormatInference(), new VariableNameInference()) { }
    public ResourceDescriptor InferResource(SurfaceValueSyntax value, LanguageSnapshot language, InferenceTrace? trace = null)
    {
        ResourceReference reference = resources.Classify(value); ResourceFormat format = formats.Infer(reference);
        TypeSymbol type = InferredType(language.Types, reference, format); string variable = names.Infer(reference);
        trace?.Add(new InferenceDecision(InferenceKind.Resource, value.Text, reference is FileResourceReference file && file.IsPattern ? "LocalFilePattern" : reference.Kind.ToString(), "resource-prefix-or-uri-shape", value.Span));
        trace?.Add(new InferenceDecision(InferenceKind.Format, value.Text, format.ToString(), "resource-extension-or-scheme", value.Span));
        trace?.Add(new InferenceDecision(InferenceKind.Type, value.Text, type.Name, "format-to-language-type", value.Span));
        trace?.Add(new InferenceDecision(InferenceKind.VariableName, value.Text, variable, "resource-name", value.Span));
        return new ResourceDescriptor(reference, format, type, variable);
    }
    private static TypeSymbol InferredType(LanguageTypeSystem types, ResourceReference reference, ResourceFormat format)
    {
        TypeSymbol type = format switch
        {
            ResourceFormat.Json => types.Json,
            ResourceFormat.Csv => types.List(types.Json),
            ResourceFormat.Xml => types.Json,
            ResourceFormat.Text => types.Text,
            ResourceFormat.Binary => types.Get<BinaryValue>(),
            ResourceFormat.Image => types.Get<ImageValue>(),
            ResourceFormat.Unknown when reference is EnvironmentResourceReference or SecretResourceReference => types.Text,
            _ => types.Object
        };
        return reference is FileResourceReference file && file.IsPattern && format != ResourceFormat.Csv ? types.List(type) : type;
    }
}
