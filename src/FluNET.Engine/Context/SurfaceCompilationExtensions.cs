using FluNET.Compilation;
using FluNET.Execution.Planning;
using FluNET.Language;
using FluNET.Language.Resources;
using FluNET.Prompt.Surface;

namespace FluNET.Context;

public sealed record SurfaceExecutionResult(SurfaceCompilationResult Compilation, object? Result, IReadOnlyList<ExecutionStepResult> Steps, Exception? Error)
{ public bool IsSuccess => Compilation.IsValid && Error is null; }

public static class SurfaceCompilationExtensions
{
    public static FluNETContext CreateSurfaceContext(Action<Microsoft.Extensions.DependencyInjection.IServiceCollection>? configureServices = null) =>
        FluNETContext.CreateWithRuntime(SurfaceLanguage.CreateRuntime(), configureServices);

    public static SurfaceCompiler GetSurfaceCompiler(this FluNETContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SurfaceCompiler(
            context.GetService<LanguageSnapshot>(), context.GetService<FluNET.Language.Binding.SemanticCommandBinder>(),
            context.GetService<TypedProgramCompiler>(), context.GetService<TypedProgramTypeValidator>(), context.GetService<ExecutionPlanner>(),
            context.GetService<IResourceProviderRegistry>());
    }

    public static SurfaceCompilationResult CompileSurface(this FluNETContext context, string source, SourceSyntaxKind syntaxKind = SourceSyntaxKind.Auto) =>
        context.GetSurfaceCompiler().Compile(new SourceDocument(source, syntaxKind));

    public static SurfaceCompilationResult CompileSurface(this FluNETContext context, SourceDocument document) =>
        context.GetSurfaceCompiler().Compile(document);

    public static async ValueTask<SurfaceExecutionResult> ExecuteSurfaceAsync(this FluNETContext context, string source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context); SurfaceCompilationResult compilation = context.CompileSurface(source);
        return await ExecuteCompiledSurfaceAsync(context, compilation, cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask<SurfaceExecutionResult> ExecuteSurfaceAsync(this FluNETContext context, SourceDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(document);
        SurfaceCompilationResult compilation = context.CompileSurface(document);
        return await ExecuteCompiledSurfaceAsync(context, compilation, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<SurfaceExecutionResult> ExecuteCompiledSurfaceAsync(FluNETContext context, SurfaceCompilationResult compilation, CancellationToken cancellationToken)
    {
        if (!compilation.IsValid || compilation.Plan is null) return new SurfaceExecutionResult(compilation, null, [], null);
        List<ExecutionStepResult> steps = [];
        try { object? result = await context.GetService<SentenceExecutor>().ExecuteAsync(compilation.Plan, steps, cancellationToken).ConfigureAwait(false); return new SurfaceExecutionResult(compilation, result, steps, null); }
        catch (Exception exception) { return new SurfaceExecutionResult(compilation, null, steps, exception); }
    }
}
