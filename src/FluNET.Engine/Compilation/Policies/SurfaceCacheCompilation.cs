using FluNET.Execution.Commands;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Prompt;
using FluNET.Prompt.Surface;

namespace FluNET.Compilation.Policies;

public sealed record SurfaceCacheCompilationResult(
    SurfaceParseResult Parse,
    IReadOnlyDictionary<SourceSpan, ExecutionCachePolicy> Assignments,
    IReadOnlyList<SurfaceDiagnostic> Diagnostics)
{
    public bool IsValid => Diagnostics.Count == 0;
}

public sealed class SurfaceCacheCompiler
{
    public SurfaceCacheCompilationResult Compile(SurfaceParseResult parse)
    {
        List<SurfaceDiagnostic> diagnostics = [.. parse.Diagnostics];
        Dictionary<SourceSpan, ExecutionCachePolicy> assignments = [];
        List<SurfaceStatementSyntax> statements = Rewrite(parse.Program.Statements, assignments, diagnostics);
        SourceSpan span = statements.Count == 0 ? default : SourceSpan.FromBounds(statements[0].Span.Start, statements[^1].Span.End);
        return new SurfaceCacheCompilationResult(
            new SurfaceParseResult(parse.Document, new SurfaceProgramSyntax(statements, span), diagnostics), assignments, diagnostics);
    }

    private static List<SurfaceStatementSyntax> Rewrite(
        IEnumerable<SurfaceStatementSyntax> statements,
        IDictionary<SourceSpan, ExecutionCachePolicy> assignments,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        List<SurfaceStatementSyntax> result = [];
        foreach (SurfaceStatementSyntax statement in statements)
        {
            switch (statement)
            {
                case SurfaceContextSyntax context:
                    result.Add(context with { Statements = Rewrite(context.Statements, assignments, diagnostics) });
                    break;
                case SurfacePolicyContextSyntax policy:
                    result.Add(policy with { Statements = Rewrite(policy.Statements, assignments, diagnostics) });
                    break;
                case SurfacePipelineSyntax pipeline:
                    result.Add(pipeline with { Stages = pipeline.Stages.Select(stage => RewriteCommand(stage, assignments, diagnostics)).ToArray() });
                    break;
                case SurfaceCommandSyntax command:
                    result.Add(RewriteCommand(command, assignments, diagnostics));
                    break;
                default:
                    result.Add(statement);
                    break;
            }
        }
        return result;
    }

    private static SurfaceCommandSyntax RewriteCommand(
        SurfaceCommandSyntax command,
        IDictionary<SourceSpan, ExecutionCachePolicy> assignments,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (command.Values.Count == 0) return command;
        SurfaceValueSyntax last = command.Values[^1];
        int marker = last.Text.LastIndexOf(" CACHE ", StringComparison.OrdinalIgnoreCase);
        if (marker <= 0) return command;
        string duration = last.Text[(marker + 7)..].Trim();
        if (!TryDuration(duration, out TimeSpan ttl))
        {
            diagnostics.Add(new SurfaceDiagnostic("FLN300", $"CACHE duration '{duration}' is invalid.", last.Span));
            return command;
        }
        string stripped = last.Text[..marker].TrimEnd();
        SurfaceValueSyntax[] values = [.. command.Values.Take(command.Values.Count - 1), new SurfaceValueSyntax(stripped, last.Span)];
        SurfaceCommandSyntax rewritten = command with { Values = values };
        assignments[rewritten.Span] = new ExecutionCachePolicy(ttl);
        return rewritten;
    }

    private static bool TryDuration(string value, out TimeSpan duration)
    {
        string text = value.Trim().ToLowerInvariant();
        (string Number, double Seconds) part = text switch
        {
            _ when text.EndsWith("ms") => (text[..^2], .001),
            _ when text.EndsWith('s') => (text[..^1], 1),
            _ when text.EndsWith('m') => (text[..^1], 60),
            _ when text.EndsWith('h') => (text[..^1], 3600),
            _ when text.EndsWith('d') => (text[..^1], 86400),
            _ => (string.Empty, 0)
        };
        if (part.Number.Length == 0 || !double.TryParse(part.Number, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double number) || number <= 0)
        { duration = default; return false; }
        double seconds = number * part.Seconds;
        if (!double.IsFinite(seconds) || seconds > TimeSpan.FromDays(365).TotalSeconds) { duration = default; return false; }
        duration = TimeSpan.FromSeconds(seconds); return true;
    }
}

public static class SurfaceCachePolicyPass
{
    public static void Attach(
        BoundProgram program,
        Lowering.SourceMap sourceMap,
        IReadOnlyDictionary<SourceSpan, ExecutionCachePolicy> assignments)
    {
        DefaultExecutionMetadataProvider metadata = new();
        foreach (Lowering.SourceMapEntry entry in sourceMap.Entries.Where(item => item.NodeKind == "command"))
        {
            if (!assignments.TryGetValue(entry.SourceSpan, out ExecutionCachePolicy? policy)) continue;
            BoundCommand command = program.Commands[entry.CommandIndex];
            FrameExecutionMetadata execution = metadata.Get(command.Frame);
            if (execution.Effect is not (ExecutionEffect.Read or ExecutionEffect.Pure))
                throw new CommandCompilationException("FLN301", $"CACHE is not allowed for {execution.Effect} command '{command.Frame.Id}'.", command.Syntax.Span);
            bool variableInput = command.Arguments.Values
                .Where(argument => argument.Slot.Direction == SlotDirection.Input)
                .SelectMany(argument => argument.Tokens)
                .Any(token => token.Kind == PromptTokenKind.Variable);
            if (variableInput)
                throw new CommandCompilationException("FLN302", "CACHE currently requires literal/resource inputs so its cache key is complete.", command.Syntax.Span);
            CommandExecutionArtifactStore.SetCache(command, policy);
        }
    }
}
