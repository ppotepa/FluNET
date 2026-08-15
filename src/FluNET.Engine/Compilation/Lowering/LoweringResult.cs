using FluNET.Compilation.Inference;
using FluNET.Prompt;
using FluNET.Prompt.Surface;

namespace FluNET.Compilation.Lowering;

public sealed record LoweringResult(
    SourceDocument Document,
    SurfaceProgramSyntax SurfaceProgram,
    PromptSyntax CanonicalSyntax,
    SourceMap SourceMap,
    InferenceTrace InferenceTrace,
    IReadOnlyList<SurfaceDiagnostic> Diagnostics)
{
    public bool IsValid => Diagnostics.Count == 0;
}
