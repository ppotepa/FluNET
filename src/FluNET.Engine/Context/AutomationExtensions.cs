using FluNET.Automation;
using FluNET.Prompt.Surface;

namespace FluNET.Context;

public static class AutomationExtensions
{
    public static AutomationCompiler GetAutomationCompiler(this FluNETContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new AutomationCompiler(context.GetSurfaceCompiler());
    }

    public static AutomationCompilationResult CompileAutomations(this FluNETContext context, string source) =>
        context.GetAutomationCompiler().Compile(new SourceDocument(source, SourceSyntaxKind.Compact));
}
