using FluNET.Execution.Planning;
using FluNET.Language.Binding;
using FluNET.Prompt;
using FluNET.Sentences;
using FluNET.Syntax.Validation;
using System.Collections;
using System.Collections.ObjectModel;

namespace FluNET.Compilation;

/// <summary>The explicit stages of side-effect-free FluNET compilation.</summary>
public enum CompilationPhase
{
    Parse,
    Bind,
    Validate,
    Plan
}

/// <summary>Severity attached to a compiler diagnostic.</summary>
public enum CompilationDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>A stable, source-aware compiler diagnostic.</summary>
public sealed record CompilationDiagnostic(
    string Code,
    CompilationPhase Phase,
    CompilationDiagnosticSeverity Severity,
    string Message,
    SourceSpan Span);

/// <summary>Stable diagnostic codes emitted after parsing.</summary>
public static class CompilationDiagnosticCodes
{
    public const string ParseFailure = "FLN005";
    public const string BindingFailure = "FLN110";
    public const string ValidationFailure = "FLN115";
    public const string CompatibilitySentenceFailure = "FLN116";
    public const string PlanningFailure = "FLN120";
    public const string UnknownMarker = "FLN130";
    public const string DuplicateMarker = "FLN131";
    public const string MissingRequiredRole = "FLN132";
    public const string SurplusArgument = "FLN133";
    public const string FrameMismatch = "FLN134";
}

/// <summary>
/// Ordered collection of compiler diagnostics. The bag is append-only from a
/// compilation consumer's point of view and preserves source/phase order.
/// </summary>
public sealed class DiagnosticBag : IReadOnlyList<CompilationDiagnostic>
{
    private readonly List<CompilationDiagnostic> _diagnostics = [];

    public int Count => _diagnostics.Count;
    public CompilationDiagnostic this[int index] => _diagnostics[index];
    public bool HasErrors => _diagnostics.Any(diagnostic =>
        diagnostic.Severity == CompilationDiagnosticSeverity.Error);

    public void Add(CompilationDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        _diagnostics.Add(diagnostic);
    }

    public void AddRange(IEnumerable<CompilationDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        foreach (CompilationDiagnostic diagnostic in diagnostics)
        {
            Add(diagnostic);
        }
    }

    public void Add(
        string code,
        CompilationPhase phase,
        string message,
        SourceSpan span,
        CompilationDiagnosticSeverity severity = CompilationDiagnosticSeverity.Error)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A diagnostic code is required.", nameof(code));
        }
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("A diagnostic message is required.", nameof(message));
        }

        _diagnostics.Add(new CompilationDiagnostic(
            code.Trim().ToUpperInvariant(),
            phase,
            severity,
            message,
            span));
    }

    public IEnumerator<CompilationDiagnostic> GetEnumerator() => _diagnostics.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>The canonical parsed program supplied to semantic binding.</summary>
public sealed record FluNetProgram
{
    public FluNetProgram(ProcessedPrompt prompt)
    {
        Prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
    }

    public ProcessedPrompt Prompt { get; }
    public string SourceText => Prompt.SourceText;
    public PromptSyntax Syntax => Prompt.Syntax;
}

/// <summary>Base node for the canonical semantically-bound program.</summary>
public abstract record BoundStatement(SourceSpan Span);

/// <summary>A bound command statement. Control statements can extend BoundStatement later.</summary>
public sealed record BoundCommandStatement : BoundStatement
{
    public BoundCommandStatement(BoundCommand command)
        : base((command ?? throw new ArgumentNullException(nameof(command))).Syntax.Span)
    {
        Command = command;
    }

    public BoundCommand Command { get; }
}

/// <summary>The canonical bound representation consumed by validation and planning.</summary>
public sealed record BoundProgram
{
    private readonly ReadOnlyCollection<BoundStatement> _statements;
    private readonly ReadOnlyCollection<BoundCommand> _commands;

    public BoundProgram(FluNetProgram program, IEnumerable<BoundStatement> statements)
    {
        Program = program ?? throw new ArgumentNullException(nameof(program));
        BoundStatement[] snapshot = statements?.ToArray()
            ?? throw new ArgumentNullException(nameof(statements));
        _statements = Array.AsReadOnly(snapshot);
        _commands = Array.AsReadOnly(snapshot
            .OfType<BoundCommandStatement>()
            .Select(statement => statement.Command)
            .ToArray());
    }

    public FluNetProgram Program { get; }
    public IReadOnlyList<BoundStatement> Statements => _statements;
    public IReadOnlyList<BoundCommand> Commands => _commands;

    internal static BoundProgram FromCommands(
        FluNetProgram program,
        IEnumerable<BoundCommand> commands) =>
        new(program, commands.Select(command => new BoundCommandStatement(command)));
}

/// <summary>
/// Full, side-effect-free output of FluNET compilation. It derives from the
/// compatibility PromptAnalysis view so existing Analyze consumers can keep
/// reading ValidationResult, Sentence, BoundCommands, and Plan.
/// </summary>
public record CompilationResult : PromptAnalysis
{
    public CompilationResult(
        FluNetProgram program,
        ValidationResult validationResult,
        ISentence? sentence,
        DiagnosticBag diagnosticBag,
        BoundProgram? boundProgram,
        ExecutionPlan? plan,
        CompilationPhase? failedPhase)
        : base(
            (program ?? throw new ArgumentNullException(nameof(program))).Prompt,
            validationResult ?? throw new ArgumentNullException(nameof(validationResult)),
            sentence)
    {
        Program = program;
        DiagnosticBag = diagnosticBag ?? throw new ArgumentNullException(nameof(diagnosticBag));
        BoundProgram = boundProgram;
        FailedPhase = failedPhase;
        BoundCommands = boundProgram?.Commands ?? Array.Empty<BoundCommand>();
        Plan = plan;
    }

    public FluNetProgram Program { get; }
    public BoundProgram? BoundProgram { get; }
    public DiagnosticBag DiagnosticBag { get; }
    public CompilationPhase? FailedPhase { get; }

    /// <summary>True when all four compiler phases completed without errors.</summary>
    public bool IsCompilationSuccessful =>
        FailedPhase is null &&
        !DiagnosticBag.HasErrors &&
        ValidationResult.IsValid &&
        Plan is not null;
}
