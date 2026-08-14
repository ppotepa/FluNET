using FluNET.Capabilities;
using FluNET.Compilation;
using FluNET.Context;
using FluNET.Execution.Planning;
using FluNET.Prompt;
using FluNET.Prompt.Surface;

namespace FluNET.Execution.Compensation;

public sealed record CompensationDiagnostic(string Code, string Message, SourceSpan Span);

public sealed record CompensationStepSpec(int StepIndex, string TargetPath, SourceSpan Span);

public sealed record CompensatableCompilationResult(
    SurfaceCompilationResult Compilation,
    IReadOnlyList<CompensationStepSpec> CompensationSteps,
    IReadOnlyList<CompensationDiagnostic> Diagnostics)
{
    public bool IsValid => Compilation.IsValid && Diagnostics.Count == 0;
}

/// <summary>
/// Compiles compact `... COMPENSATE` markers without introducing another command executor.
/// Built-in compensation is intentionally narrow: only one literal local SAVE per target.
/// </summary>
public sealed class CompensatableSurfaceCompiler(SurfaceCompiler compiler)
{
    public CompensatableCompilationResult Compile(string source)
    {
        SourceDocument document = new(source, SourceSyntaxKind.Compact);
        SurfaceParseResult parsed = new SurfaceParser().Parse(document);
        List<CompensationDiagnostic> diagnostics = [];
        HashSet<SourceSpan> marked = [];
        IReadOnlyList<SurfaceStatementSyntax> rewritten = RewriteStatements(parsed.Program.Statements, marked, diagnostics);
        SourceSpan programSpan = rewritten.Count == 0
            ? default
            : SourceSpan.FromBounds(rewritten[0].Span.Start, rewritten[^1].Span.End);
        SurfaceParseResult transformed = new(
            document,
            new SurfaceProgramSyntax(rewritten, programSpan),
            parsed.Diagnostics);
        SurfaceCompilationResult compilation = compiler.Compile(transformed);
        if (!compilation.IsValid || marked.Count == 0)
            return new(compilation, [], diagnostics);

        List<CompensationStepSpec> specs = [];
        foreach (var entry in compilation.Lowering.SourceMap.Entries.Where(entry => entry.NodeKind == "command" && marked.Contains(entry.SourceSpan)))
        {
            if (entry.CommandIndex < 0 || entry.CommandIndex >= compilation.Plan!.Steps.Count)
                continue;
            ExecutionPlanStep step = compilation.Plan.Steps[entry.CommandIndex];
            if (step.Command.Frame.Id != new FluNET.Language.FrameId("core.save.text"))
            {
                diagnostics.Add(new("FLN360", $"Frame '{step.Command.Frame.Id}' has no built-in compensation contract. Built-in COMPENSATE currently supports local SAVE only.", entry.SourceSpan));
                continue;
            }
            if (!TryLiteralSaveTarget(step, out string? target))
            {
                diagnostics.Add(new("FLN361", "COMPENSATE SAVE currently requires a literal local target path.", entry.SourceSpan));
                continue;
            }
            specs.Add(new(step.Index, target!, entry.SourceSpan));
        }

        string[] duplicateTargets = specs
            .GroupBy(spec => Path.GetFullPath(spec.TargetPath), PathComparer)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateTargets.Length > 0)
        {
            diagnostics.Add(new("FLN362", $"A compensatable plan may mutate each local target at most once: {string.Join(", ", duplicateTargets)}.", compilation.Lowering.CanonicalSyntax.Span));
        }
        return new(compilation, specs, diagnostics);
    }

    private static IReadOnlyList<SurfaceStatementSyntax> RewriteStatements(
        IEnumerable<SurfaceStatementSyntax> statements,
        ISet<SourceSpan> marked,
        ICollection<CompensationDiagnostic> diagnostics)
    {
        List<SurfaceStatementSyntax> result = [];
        foreach (SurfaceStatementSyntax statement in statements)
        {
            switch (statement)
            {
                case SurfaceCommandSyntax command:
                    result.Add(RewriteCommand(command, marked, diagnostics));
                    break;
                case SurfacePipelineSyntax pipeline:
                    result.Add(pipeline with
                    {
                        Stages = pipeline.Stages.Select(stage => RewriteCommand(stage, marked, diagnostics)).ToArray()
                    });
                    break;
                case SurfaceContextSyntax context:
                    result.Add(context with { Statements = RewriteStatements(context.Statements, marked, diagnostics) });
                    break;
                case SurfacePolicyContextSyntax policy:
                    result.Add(policy with { Statements = RewriteStatements(policy.Statements, marked, diagnostics) });
                    break;
                case SurfaceTaskDefinitionSyntax task:
                    result.Add(task with { Statements = RewriteStatements(task.Statements, marked, diagnostics) });
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
        ISet<SourceSpan> marked,
        ICollection<CompensationDiagnostic> diagnostics)
    {
        if (command.Values.Count == 0) return command;
        SurfaceValueSyntax last = command.Values[^1];
        if (!TryStripTerminalKeyword(last.Text, "COMPENSATE", out string? stripped)) return command;
        if (stripped!.Length == 0)
        {
            diagnostics.Add(new("FLN363", "COMPENSATE must follow a complete command value.", command.Span));
            return command;
        }
        SurfaceValueSyntax[] values = [.. command.Values.Take(command.Values.Count - 1), last with { Text = stripped }];
        SurfaceCommandSyntax rewritten = command with { Values = values };
        marked.Add(rewritten.Span);
        return rewritten;
    }

    private static bool TryStripTerminalKeyword(string source, string keyword, out string? stripped)
    {
        stripped = null;
        string text = source.TrimEnd();
        if (text.Length < keyword.Length) return false;
        int candidate = text.Length - keyword.Length;
        if (!text.AsSpan(candidate).Equals(keyword, StringComparison.OrdinalIgnoreCase) ||
            (candidate > 0 && !char.IsWhiteSpace(text[candidate - 1]))) return false;

        char? quote = null;
        bool escaped = false;
        int depth = 0;
        for (int index = 0; index < candidate; index++)
        {
            char current = text[index];
            if (escaped) { escaped = false; continue; }
            if (quote is not null)
            {
                if (current == '\\') escaped = true;
                else if (current == quote) quote = null;
                continue;
            }
            if (current is '"' or '\'') { quote = current; continue; }
            if (current is '(' or '[' or '{') depth++;
            else if (current is ')' or ']' or '}') depth = Math.Max(0, depth - 1);
        }
        if (quote is not null || depth != 0) return false;
        stripped = text[..candidate].TrimEnd();
        return true;
    }

    private static bool TryLiteralSaveTarget(ExecutionPlanStep step, out string? target)
    {
        target = null;
        PromptToken[] tokens = step.Command.Syntax.AllTokens.ToArray();
        int to = Array.FindIndex(tokens, token => token.Text.Equals("TO", StringComparison.OrdinalIgnoreCase));
        if (to < 0 || to + 1 >= tokens.Length) return false;
        PromptToken value = tokens[to + 1];
        if (value.Kind != PromptTokenKind.Reference) return false;
        string text = value.Text.Trim();
        if (text.Length >= 2 && text[0] == '{' && text[^1] == '}') text = text[1..^1];
        if (text.Contains('{') || text.Contains('[')) return false;
        if (Uri.TryCreate(text, UriKind.Absolute, out _)) return false;
        target = text;
        return !string.IsNullOrWhiteSpace(target);
    }

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
}

public sealed record CompensationActionResult(
    int StepIndex,
    string TargetPath,
    bool Restored,
    Exception? Error);

public sealed record CompensationExecutionResult(
    CompensatableCompilationResult Compilation,
    IReadOnlyList<ExecutionStepResult> Steps,
    IReadOnlyList<CompensationActionResult> Compensation,
    object? Result,
    Exception? Error)
{
    public bool IsSuccess => Compilation.IsValid && Error is null;
    public bool WasCompensated => Compensation.Any(action => action.Restored);
}

public sealed class CompensationCoordinator(
    ExecutionPlanExecutor executor,
    IFluNetFileSystem files)
{
    private sealed record Snapshot(bool Existed, string? Content);

    public async ValueTask<CompensationExecutionResult> ExecuteAsync(
        CompensatableCompilationResult compilation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        List<ExecutionStepResult> steps = [];
        List<CompensationActionResult> compensation = [];
        if (!compilation.IsValid || compilation.Compilation.Plan is null)
            return new(compilation, steps, compensation, null, new InvalidOperationException("Compensatable compilation is invalid."));

        Dictionary<int, Snapshot> snapshots = [];
        foreach (CompensationStepSpec spec in compilation.CompensationSteps)
        {
            bool exists = await files.FileExistsAsync(spec.TargetPath, cancellationToken).ConfigureAwait(false);
            string? content = exists
                ? await files.ReadAllTextAsync(spec.TargetPath, cancellationToken).ConfigureAwait(false)
                : null;
            snapshots[spec.StepIndex] = new(exists, content);
        }

        try
        {
            object? result = await executor.ExecuteAsync(compilation.Compilation.Plan, steps, cancellationToken).ConfigureAwait(false);
            return new(compilation, steps, compensation, result, null);
        }
        catch (Exception failure)
        {
            HashSet<int> succeeded = steps
                .Where(step => step.Status == Execution.Workflow.WorkflowStepStatus.Succeeded)
                .Select(step => step.Step.Index)
                .ToHashSet();
            foreach (CompensationStepSpec spec in compilation.CompensationSteps
                .Where(spec => succeeded.Contains(spec.StepIndex))
                .OrderByDescending(spec => spec.StepIndex))
            {
                Snapshot snapshot = snapshots[spec.StepIndex];
                try
                {
                    if (snapshot.Existed)
                        await files.WriteAllTextAsync(spec.TargetPath, snapshot.Content ?? string.Empty, CancellationToken.None).ConfigureAwait(false);
                    else
                        await files.DeleteFileAsync(spec.TargetPath, CancellationToken.None).ConfigureAwait(false);
                    compensation.Add(new(spec.StepIndex, spec.TargetPath, true, null));
                }
                catch (Exception compensationFailure)
                {
                    compensation.Add(new(spec.StepIndex, spec.TargetPath, false, compensationFailure));
                }
            }
            return new(compilation, steps, compensation, null, failure);
        }
    }
}
