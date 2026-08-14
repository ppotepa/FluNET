using FluNET.Compilation.Dependencies;
using FluNET.Compilation.Lowering;
using FluNET.Compilation.Policies;
using FluNET.Compilation.Tasks;
using FluNET.Execution.Planning;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Resources;
using FluNET.Prompt;
using FluNET.Prompt.Surface;

namespace FluNET.Compilation;

public sealed record SurfaceCompilationResult(SourceDocument Document, SurfaceParseResult SurfaceParse, LoweringResult Lowering, DiagnosticBag Diagnostics, BoundProgram? BoundProgram, TypedProgram? TypedProgram, DependencyGraph? DependencyGraph, ExecutionPlan? Plan, CompilationPhase? FailedPhase)
{ public bool IsValid => SurfaceParse.IsValid && Lowering.IsValid && !Diagnostics.HasErrors && TypedProgram is not null && DependencyGraph is not null && Plan is not null && FailedPhase is null; }

public sealed class SurfaceCompiler(LanguageSnapshot language, SemanticCommandBinder binder, TypedProgramCompiler typedCompiler, TypedProgramTypeValidator typeValidator, ExecutionPlanner planner, IResourceProviderRegistry resourceProviders)
{
    private readonly SemanticProgramValidator _semanticValidator = new(language); private readonly DependencyAnalyzer _dependencies = new();
    public SurfaceCompilationResult Compile(SourceDocument document) => Compile(new SurfaceParser().Parse(document));
    public SurfaceCompilationResult Compile(SourceDocument document, SurfaceProgramSyntax program) =>
        Compile(new SurfaceParseResult(document, program, Array.Empty<SurfaceDiagnostic>()));

    public SurfaceCompilationResult Compile(SurfaceParseResult raw)
    {
        SourceDocument document = raw.Document;
        DiagnosticBag diagnostics = new();
        SurfaceTaskCompilationResult tasks = new SurfaceTaskCompiler(language).Compile(raw);
        SurfacePolicyCompilationResult policies = new SurfacePolicyCompiler().Compile(tasks.Parse);
        SurfaceCacheCompilationResult cache = new SurfaceCacheCompiler().Compile(policies.Parse);
        SurfaceIdempotencyCompilationResult idempotency = new SurfaceIdempotencyCompiler().Compile(cache.Parse);
        SurfaceParseResult parsed = idempotency.Parse;
        LoweringResult lowered = new SurfaceLowerer(resourceProviders).Lower(parsed, language.Grammar, language);
        lowered = SurfacePolicyApplicationPass.Apply(lowered, policies.Assignments, language.Grammar);
        if (!lowered.IsValid || !tasks.IsValid || !policies.IsValid || !cache.IsValid || !idempotency.IsValid) return new(document, parsed, lowered, diagnostics, null, null, null, null, CompilationPhase.Parse);
        BoundProgram bound;
        try
        {
            IReadOnlyList<BoundCommand> commands = binder.BindProgram(lowered.CanonicalSyntax);
            bound = BoundProgram.FromCommands(new FluNetProgram(new ProcessedPrompt(document.Text, language.Grammar), lowered.CanonicalSyntax), commands);
            SurfaceCachePolicyPass.Attach(bound, lowered.SourceMap, cache.Assignments);
            SurfaceIdempotencyPolicyPass.Attach(bound, lowered.SourceMap, idempotency.Assignments);
        }
        catch (SemanticBindingException exception) { diagnostics.Add(CompilationDiagnosticCodes.BindingFailure, CompilationPhase.Bind, exception.Message, exception.Span); return new(document, parsed, lowered, diagnostics, null, null, null, null, CompilationPhase.Bind); }
        catch (CommandCompilationException exception) { diagnostics.Add(exception.Code, CompilationPhase.Compile, exception.Message, exception.Span); return new(document, parsed, lowered, diagnostics, bound, null, null, null, CompilationPhase.Compile); }
        DiagnosticBag semantic = _semanticValidator.Validate(bound); diagnostics.AddRange(semantic); if (semantic.HasErrors) return new(document, parsed, lowered, diagnostics, bound, null, null, null, CompilationPhase.Validate);
        TypedProgram typed; try { typed = typedCompiler.Compile(bound); } catch (CommandCompilationException exception) { diagnostics.Add(exception.Code, CompilationPhase.Compile, exception.Message, exception.Span); return new(document, parsed, lowered, diagnostics, bound, null, null, null, CompilationPhase.Compile); }
        try { typeValidator.Validate(typed); } catch (CommandCompilationException exception) { diagnostics.Add(exception.Code, CompilationPhase.TypeCheck, exception.Message, exception.Span); return new(document, parsed, lowered, diagnostics, bound, typed, null, null, CompilationPhase.TypeCheck); }
        DependencyGraph graph; try { graph = _dependencies.Analyze(bound, lowered.CanonicalSyntax, lowered.InferenceTrace); } catch (CommandCompilationException exception) { diagnostics.Add(exception.Code, CompilationPhase.TypeCheck, exception.Message, exception.Span); return new(document, parsed, lowered, diagnostics, bound, typed, null, null, CompilationPhase.TypeCheck); }
        try { ExecutionPlan plan = planner.Create(graph); return new(document, parsed, lowered, diagnostics, bound, typed, graph, plan, null); } catch (Exception exception) when (exception is ExecutionPlanException or FormatException or InvalidOperationException) { diagnostics.Add(CompilationDiagnosticCodes.PlanningFailure, CompilationPhase.Plan, exception.Message, lowered.CanonicalSyntax.Span); return new(document, parsed, lowered, diagnostics, bound, typed, graph, null, CompilationPhase.Plan); }
    }
}
