using FluNET.Declarative;

namespace FluNET.Context;

public static class DesiredStateExtensions
{
    public static EnsureCompiler GetEnsureCompiler(this FluNETContext context) =>
        new(context.GetSurfaceCompiler());
    public static DesiredStateCompilationResult CompileEnsure(this FluNETContext context, string source) =>
        context.GetEnsureCompiler().Compile(source);
}
