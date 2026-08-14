using FluNET.Execution.Commands;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Prompt;
using FluNET.Prompt.Surface;

namespace FluNET.Compilation.Policies;

public sealed record SurfaceIdempotencyCompilationResult(
    SurfaceParseResult Parse,
    IReadOnlyDictionary<SourceSpan, ExecutionIdempotencyPolicy> Assignments,
    IReadOnlyList<SurfaceDiagnostic> Diagnostics)
{ public bool IsValid => Diagnostics.Count == 0; }

public sealed class SurfaceIdempotencyCompiler
{
    public SurfaceIdempotencyCompilationResult Compile(SurfaceParseResult parse)
    {
        List<SurfaceDiagnostic> diagnostics = [.. parse.Diagnostics];
        Dictionary<SourceSpan, ExecutionIdempotencyPolicy> assignments = [];
        List<SurfaceStatementSyntax> statements = Rewrite(parse.Program.Statements, assignments, diagnostics);
        SourceSpan span = statements.Count == 0 ? default : SourceSpan.FromBounds(statements[0].Span.Start, statements[^1].Span.End);
        return new(new SurfaceParseResult(parse.Document, new SurfaceProgramSyntax(statements, span), diagnostics), assignments, diagnostics);
    }

    private static List<SurfaceStatementSyntax> Rewrite(IEnumerable<SurfaceStatementSyntax> statements,
        IDictionary<SourceSpan, ExecutionIdempotencyPolicy> assignments, ICollection<SurfaceDiagnostic> diagnostics)
    {
        List<SurfaceStatementSyntax> result = [];
        foreach (SurfaceStatementSyntax statement in statements)
        {
            switch (statement)
            {
                case SurfaceContextSyntax context: result.Add(context with { Statements = Rewrite(context.Statements, assignments, diagnostics) }); break;
                case SurfacePolicyContextSyntax policy: result.Add(policy with { Statements = Rewrite(policy.Statements, assignments, diagnostics) }); break;
                case SurfacePipelineSyntax pipeline: result.Add(pipeline with { Stages = pipeline.Stages.Select(stage => RewriteCommand(stage, assignments, diagnostics)).ToArray() }); break;
                case SurfaceCommandSyntax command: result.Add(RewriteCommand(command, assignments, diagnostics)); break;
                default: result.Add(statement); break;
            }
        }
        return result;
    }

    private static SurfaceCommandSyntax RewriteCommand(SurfaceCommandSyntax command,
        IDictionary<SourceSpan, ExecutionIdempotencyPolicy> assignments, ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (command.Values.Count == 0) return command;
        SurfaceValueSyntax last = command.Values[^1];
        int marker = last.Text.LastIndexOf(" ONCE BY ", StringComparison.OrdinalIgnoreCase);
        if (marker <= 0) return command;
        string key = last.Text[(marker + 9)..].Trim();
        if (key.Length == 0)
        { diagnostics.Add(new SurfaceDiagnostic("FLN303", "ONCE BY requires a key expression.", last.Span)); return command; }
        string stripped = last.Text[..marker].TrimEnd();
        SurfaceValueSyntax[] values = [.. command.Values.Take(command.Values.Count - 1), new SurfaceValueSyntax(stripped, last.Span)];
        SurfaceCommandSyntax rewritten = command with { Values = values };
        assignments[rewritten.Span] = new ExecutionIdempotencyPolicy(key);
        return rewritten;
    }
}

public static class SurfaceIdempotencyPolicyPass
{
    public static void Attach(BoundProgram program, Lowering.SourceMap sourceMap,
        IReadOnlyDictionary<SourceSpan, ExecutionIdempotencyPolicy> assignments)
    {
        DefaultExecutionMetadataProvider metadata = new();
        foreach (Lowering.SourceMapEntry entry in sourceMap.Entries.Where(item => item.NodeKind == "command"))
        {
            if (!assignments.TryGetValue(entry.SourceSpan, out ExecutionIdempotencyPolicy? policy)) continue;
            BoundCommand command = program.Commands[entry.CommandIndex];
            ExecutionEffect effect = metadata.Get(command.Frame).Effect;
            if (effect is not (ExecutionEffect.Write or ExecutionEffect.ExternalMutation))
                throw new CommandCompilationException("FLN304", $"ONCE BY is only valid for effectful commands, not {effect} '{command.Frame.Id}'.", command.Syntax.Span);
            if (DynamicPathExpression.TryParse(policy.KeyExpression, out DynamicPathExpression? path))
            {
                string root = path!.Root;
                bool producedOrInput = command.Arguments.Values.SelectMany(argument => argument.Tokens)
                    .Any(token => token.Kind == PromptTokenKind.Variable && token.Text.Trim('[', ']', '.').Equals(root, StringComparison.OrdinalIgnoreCase));
                // Cross-command/host variables are allowed; runtime evaluation remains authoritative.
                _ = producedOrInput;
            }
            CommandExecutionArtifactStore.SetIdempotency(command, policy);
        }
    }
}
