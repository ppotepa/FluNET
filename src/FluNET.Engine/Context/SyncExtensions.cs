using FluNET.Declarative.Reconciliation;
using FluNET.Language;

namespace FluNET.Context;

public static class SyncExtensions
{
    public static SyncCompiler GetSyncCompiler(this FluNETContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SyncCompiler(
            context.GetSurfaceCompiler(),
            context.GetService<LanguageSnapshot>());
    }

    public static SyncCompilationResult CompileSync(this FluNETContext context, string source)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.GetSyncCompiler().Compile(source);
    }
}
