using FluNET.Execution.Compensation;

namespace FluNET.Context;

public static class CompensationExtensions
{
    public static CompensatableSurfaceCompiler GetCompensatableSurfaceCompiler(this FluNETContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new CompensatableSurfaceCompiler(context.GetSurfaceCompiler());
    }

    public static CompensatableCompilationResult CompileCompensatableSurface(
        this FluNETContext context,
        string source)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.GetCompensatableSurfaceCompiler().Compile(source);
    }

    public static CompensationCoordinator GetCompensationCoordinator(this FluNETContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new CompensationCoordinator(
            context.GetService<FluNET.Execution.Planning.SentenceExecutor>(),
            context.GetService<FluNET.Capabilities.IFluNetFileSystem>());
    }

    public static ValueTask<CompensationExecutionResult> ExecuteCompensatableSurfaceAsync(
        this FluNETContext context,
        string source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        CompensatableCompilationResult compilation = context.CompileCompensatableSurface(source);
        return context.GetCompensationCoordinator().ExecuteAsync(compilation, cancellationToken);
    }
}
