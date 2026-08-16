using FluNET.Automation;
using FluNET.Prompt.Surface;
using FluNET.Variables;

namespace FluNET.Context;

public static class AutomationExtensions
{
    public static AutomationCompiler GetAutomationCompiler(this FluNETContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new AutomationCompiler(context.GetSurfaceCompiler());
    }

    public static AutomationCompilationResult CompileAutomations(this FluNETContext context, string source)
    {
        // WATCH workflows receive these host inputs at runtime. Registering
        // typed placeholders lets the compiler validate their references
        // before a concrete filesystem/network event exists.
        IVariableResolver variables = context.GetService<IVariableResolver>();
        if (!variables.IsRegistered("event"))
        {
            variables.Register("event", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["resource"] = string.Empty,
                ["name"] = string.Empty,
                ["kind"] = string.Empty,
                ["path"] = string.Empty,
                ["oldPath"] = string.Empty,
                ["timestamp"] = string.Empty,
                ["isDirectory"] = string.Empty,
                ["length"] = string.Empty
            });
        }
        return context.GetAutomationCompiler().Compile(new SourceDocument(source, SourceSyntaxKind.Compact));
    }
}
