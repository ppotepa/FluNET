using FluNET.Compilation.Lowering;
using FluNET.Execution.Workflow;
using FluNET.Prompt;
using FluNET.Prompt.Surface;

namespace FluNET.Compilation.Policies;

public sealed record SurfacePolicyProfile(int? Retry, string? Timeout, bool ContinueOnError)
{
    public static SurfacePolicyProfile Empty { get; } = new(null, null, false);
    public SurfacePolicyProfile Merge(SurfacePolicyProfile other) => new(
        other.Retry ?? Retry,
        other.Timeout ?? Timeout,
        ContinueOnError || other.ContinueOnError);
}

public sealed record SurfacePolicyCompilationResult(
    SurfaceParseResult Parse,
    IReadOnlyDictionary<SourceSpan, SurfacePolicyProfile> Assignments,
    IReadOnlyList<SurfaceDiagnostic> Diagnostics)
{
    public bool IsValid => Diagnostics.Count == 0;
}

/// <summary>Compiles POLICY declarations and WITH/USING applications without creating runtime commands.</summary>
public sealed class SurfacePolicyCompiler
{
    public SurfacePolicyCompilationResult Compile(SurfaceParseResult parse)
    {
        ArgumentNullException.ThrowIfNull(parse);
        Dictionary<string, SurfacePolicyProfile> profiles = new(StringComparer.OrdinalIgnoreCase);
        List<SurfaceDiagnostic> diagnostics = [.. parse.Diagnostics];
        CollectDefinitions(parse.Program.Statements, profiles, diagnostics);
        Dictionary<SourceSpan, SurfacePolicyProfile> assignments = [];
        List<SurfaceStatementSyntax> executable = Rewrite(parse.Program.Statements, profiles, SurfacePolicyProfile.Empty, assignments, diagnostics);
        SourceSpan span = executable.Count == 0 ? default : SourceSpan.FromBounds(executable[0].Span.Start, executable[^1].Span.End);
        SurfaceParseResult transformed = new(parse.Document, new SurfaceProgramSyntax(executable, span), diagnostics);
        return new SurfacePolicyCompilationResult(transformed, assignments, diagnostics);
    }

    private static void CollectDefinitions(
        IEnumerable<SurfaceStatementSyntax> statements,
        IDictionary<string, SurfacePolicyProfile> profiles,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        foreach (SurfaceStatementSyntax statement in statements)
        {
            if (statement is SurfacePolicyDefinitionSyntax definition)
            {
                if (profiles.ContainsKey(definition.Name))
                {
                    diagnostics.Add(new SurfaceDiagnostic("FLN285", $"Policy '{definition.Name}' is declared more than once.", definition.Span));
                    continue;
                }
                profiles[definition.Name] = ParseProfile(definition, diagnostics);
                continue;
            }
            if (statement is SurfaceContextSyntax context) CollectDefinitions(context.Statements, profiles, diagnostics);
            if (statement is SurfacePolicyContextSyntax policyContext) CollectDefinitions(policyContext.Statements, profiles, diagnostics);
        }
    }

    private static SurfacePolicyProfile ParseProfile(SurfacePolicyDefinitionSyntax definition, ICollection<SurfaceDiagnostic> diagnostics)
    {
        int? retry = null;
        string? timeout = null;
        bool continueOnError = false;
        foreach (SurfaceStatementSyntax statement in definition.Statements)
        {
            if (statement is not SurfaceCommandSyntax command)
            {
                diagnostics.Add(new SurfaceDiagnostic("FLN286", "POLICY bodies may contain only RETRY, TIMEOUT and CONTINUE.", statement.Span));
                continue;
            }
            string value = command.Values.Count == 1 ? command.Values[0].UnquotedText.Trim() : string.Empty;
            switch (command.NormalizedName)
            {
                case "RETRY" when int.TryParse(value, out int parsed) && parsed >= 0 && parsed <= 100:
                    retry = parsed;
                    break;
                case "TIMEOUT" when value.Length > 0:
                    timeout = value;
                    break;
                case "CONTINUE" when value.Length == 0 || value.Equals("ON ERROR", StringComparison.OrdinalIgnoreCase):
                    continueOnError = true;
                    break;
                case "BACKOFF":
                    diagnostics.Add(new SurfaceDiagnostic("FLN287", "BACKOFF is reserved until CommandExecutionPolicy has a backoff contract.", command.Span));
                    break;
                case "CONTINUE":
                    diagnostics.Add(new SurfaceDiagnostic("FLN288", "Status-specific CONTINUE is not available yet; use CONTINUE or CONTINUE ON ERROR.", command.Span));
                    break;
                default:
                    diagnostics.Add(new SurfaceDiagnostic("FLN286", $"Unsupported policy directive '{command.Name}'.", command.Span));
                    break;
            }
        }
        return new SurfacePolicyProfile(retry, timeout, continueOnError);
    }

    private static List<SurfaceStatementSyntax> Rewrite(
        IEnumerable<SurfaceStatementSyntax> statements,
        IReadOnlyDictionary<string, SurfacePolicyProfile> profiles,
        SurfacePolicyProfile inherited,
        IDictionary<SourceSpan, SurfacePolicyProfile> assignments,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        List<SurfaceStatementSyntax> output = [];
        foreach (SurfaceStatementSyntax statement in statements)
        {
            if (statement is SurfacePolicyDefinitionSyntax) continue;
            if (statement is SurfacePolicyContextSyntax context)
            {
                if (!profiles.TryGetValue(context.Name, out SurfacePolicyProfile? profile))
                {
                    diagnostics.Add(new SurfaceDiagnostic("FLN289", $"Unknown policy profile '{context.Name}'.", context.Span));
                    continue;
                }
                output.AddRange(Rewrite(context.Statements, profiles, inherited.Merge(profile), assignments, diagnostics));
                continue;
            }
            if (statement is SurfaceContextSyntax resourceContext)
            {
                List<SurfaceStatementSyntax> children = Rewrite(resourceContext.Statements, profiles, inherited, assignments, diagnostics);
                output.Add(resourceContext with { Statements = children });
                continue;
            }
            if (statement is SurfacePipelineSyntax pipeline)
            {
                SurfaceCommandSyntax[] stages = pipeline.Stages.Select(stage => RewriteCommand(stage, profiles, inherited, assignments, diagnostics)).ToArray();
                output.Add(pipeline with { Stages = stages });
                continue;
            }
            if (statement is SurfaceCommandSyntax command)
            {
                output.Add(RewriteCommand(command, profiles, inherited, assignments, diagnostics));
                continue;
            }
            output.Add(statement);
        }
        return output;
    }

    private static SurfaceCommandSyntax RewriteCommand(
        SurfaceCommandSyntax command,
        IReadOnlyDictionary<string, SurfacePolicyProfile> profiles,
        SurfacePolicyProfile inherited,
        IDictionary<SourceSpan, SurfacePolicyProfile> assignments,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        SurfacePolicyProfile effective = inherited;
        SurfaceCommandSyntax rewritten = command;
        if (command.Values.Count > 0)
        {
            SurfaceValueSyntax last = command.Values[^1];
            string text = last.Text;
            int usingIndex = text.LastIndexOf(" USING ", StringComparison.OrdinalIgnoreCase);
            if (usingIndex > 0)
            {
                string profileName = text[(usingIndex + 7)..].Trim();
                if (!profiles.TryGetValue(profileName, out SurfacePolicyProfile? profile))
                    diagnostics.Add(new SurfaceDiagnostic("FLN289", $"Unknown policy profile '{profileName}'.", last.Span));
                else
                    effective = effective.Merge(profile);
                string stripped = text[..usingIndex].TrimEnd();
                SurfaceValueSyntax[] values = [.. command.Values.Take(command.Values.Count - 1), new SurfaceValueSyntax(stripped, last.Span)];
                rewritten = command with { Values = values };
            }
        }
        if (effective != SurfacePolicyProfile.Empty) assignments[rewritten.Span] = effective;
        return rewritten;
    }
}

public static class SurfacePolicyApplicationPass
{
    public static LoweringResult Apply(
        LoweringResult lowering,
        IReadOnlyDictionary<SourceSpan, SurfacePolicyProfile> assignments,
        PromptGrammar grammar)
    {
        if (assignments.Count == 0) return lowering;
        CommandSyntax[] commands = lowering.CanonicalSyntax.Commands.ToArray();
        foreach (SourceMapEntry entry in lowering.SourceMap.Entries.Where(item => item.NodeKind == "command"))
        {
            if (!assignments.TryGetValue(entry.SourceSpan, out SurfacePolicyProfile? profile)) continue;
            commands[entry.CommandIndex] = Apply(commands[entry.CommandIndex], profile, grammar, entry.SourceSpan.Start);
        }
        return lowering with { CanonicalSyntax = new PromptSyntax(commands, lowering.CanonicalSyntax.Links) };
    }

    private static CommandSyntax Apply(CommandSyntax command, SurfacePolicyProfile profile, PromptGrammar grammar, int position)
    {
        List<PromptToken> tokens = [.. command.AllTokens];
        if (profile.Retry is int retry)
        {
            tokens.Add(Token("WITH", position)); tokens.Add(Token("RETRY", position)); tokens.Add(Token(retry.ToString(System.Globalization.CultureInfo.InvariantCulture), position));
        }
        if (profile.Timeout is string timeout)
        {
            tokens.Add(Token("WITH", position)); tokens.Add(Token("TIMEOUT", position)); tokens.Add(Token(timeout, position));
        }
        if (profile.ContinueOnError)
        {
            tokens.Add(Token("ON", position)); tokens.Add(Token("ERROR", position)); tokens.Add(Token("CONTINUE", position));
        }
        return new CommandSyntax(tokens, grammar);
    }

    private static PromptToken Token(string text, int position) => new(text, PromptTokenKind.Word, Math.Max(0, position), 0);
}
