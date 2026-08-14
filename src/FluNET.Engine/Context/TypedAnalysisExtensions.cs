using FluNET.Compilation;
using FluNET.Prompt;

namespace FluNET.Context;

/// <summary>
/// Side-effect-free 0.4 analysis result layered on the source-compatible 0.3
/// CompilationResult. Typed command compilation and type checking determine validity.
/// </summary>
public sealed record TypedAnalysisResult(
    CompilationResult Analysis,
    TypedProgram? TypedProgram,
    CommandCompilationException? CompilationError)
{
    public bool IsValid =>
        Analysis.IsValid &&
        TypedProgram is not null &&
        CompilationError is null;
}

public static class TypedAnalysisExtensions
{
    public static TypedAnalysisResult AnalyzeTyped(
        this FluNETContext context,
        ProcessedPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(prompt);

        CompilationResult analysis = context.GetEngine().Analyze(prompt);
        if (!analysis.IsValid || analysis.BoundProgram is null)
        {
            return new TypedAnalysisResult(analysis, null, null);
        }

        try
        {
            TypedProgram typed = context
                .GetService<TypedProgramCompiler>()
                .Compile(analysis.BoundProgram);
            context.GetService<TypedProgramTypeValidator>().Validate(typed);
            return new TypedAnalysisResult(analysis, typed, null);
        }
        catch (CommandCompilationException exception)
        {
            return new TypedAnalysisResult(analysis, null, exception);
        }
    }
}
