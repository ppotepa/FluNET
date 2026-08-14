using FluNET.Compilation.Inference;
using FluNET.Language;
using FluNET.Language.Resources;
using FluNET.Prompt;
using FluNET.Prompt.Expressions;
using FluNET.Prompt.Surface;

namespace FluNET.Compilation.Lowering;

public sealed record LoweringResult(
    SourceDocument Document,
    SurfaceProgramSyntax SurfaceProgram,
    PromptSyntax CanonicalSyntax,
    SourceMap SourceMap,
    InferenceTrace InferenceTrace,
    IReadOnlyList<SurfaceDiagnostic> Diagnostics)
{
    public bool IsValid => Diagnostics.Count == 0;
}

public sealed class SurfaceLowerer
{
    private readonly InferenceEngine _inference;

    public SurfaceLowerer() : this(new InferenceEngine()) { }

    public SurfaceLowerer(InferenceEngine inference)
    {
        _inference = inference ?? throw new ArgumentNullException(nameof(inference));
    }

    public LoweringResult Lower(
        SurfaceParseResult parse,
        PromptGrammar grammar,
        LanguageSnapshot? language = null)
    {
        ArgumentNullException.ThrowIfNull(parse);
        ArgumentNullException.ThrowIfNull(grammar);
        language ??= StandardLanguage.CreateSnapshot();
        List<CommandSyntax> commands = [];
        List<CommandLinkSyntax> links = [];
        List<SourceMapEntry> map = [];
        List<SurfaceDiagnostic> diagnostics = [.. parse.Diagnostics];
        InferenceTrace trace = new();

        foreach (SurfaceStatementSyntax statement in parse.Program.Statements)
        {
            if (statement is not SurfaceCommandSyntax command)
            {
                diagnostics.Add(new SurfaceDiagnostic("FLN210",
                    $"Unsupported surface statement '{statement.GetType().Name}'.", statement.Span));
                continue;
            }
            IReadOnlyList<CommandSyntax> lowered = command.NormalizedName switch
            {
                "SAY" => [LowerSay(command, grammar)],
                "LOAD" => LowerLoad(command, grammar, language, trace, diagnostics),
                _ => []
            };
            if (lowered.Count == 0)
            {
                if (!diagnostics.Any(item => item.Span == command.Span))
                {
                    diagnostics.Add(new SurfaceDiagnostic("FLN211",
                        $"Surface command '{command.Name}' does not have a lowering rule yet.", command.Span));
                }
                continue;
            }
            for (int offset = 0; offset < lowered.Count; offset++)
            {
                int commandIndex = commands.Count;
                commands.Add(lowered[offset]);
                map.Add(new SourceMapEntry(commandIndex, "command", command.Span));
                if (offset > 0)
                {
                    links.Add(Link(commandIndex - 1, commandIndex, CommandLinkKind.Parallel, command.Span.Start, "AND"));
                }
            }
        }

        return new LoweringResult(parse.Document, parse.Program, new PromptSyntax(commands, links),
            new SourceMap(map), trace, diagnostics);
    }

    private IReadOnlyList<CommandSyntax> LowerLoad(
        SurfaceCommandSyntax command,
        PromptGrammar grammar,
        LanguageSnapshot language,
        InferenceTrace trace,
        ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (command.Values.Count == 0)
        {
            diagnostics.Add(new SurfaceDiagnostic("FLN220", "LOAD requires at least one resource.", command.Span));
            return [];
        }
        if (command.Values.Count > 1 && command.Alias is not null)
        {
            diagnostics.Add(new SurfaceDiagnostic("FLN221",
                "AS on multiple explicit LOAD resources is reserved for collection/glob lowering.", command.Span));
            return [];
        }
        List<CommandSyntax> result = [];
        foreach (SurfaceValueSyntax value in command.Values)
        {
            ResourceDescriptor descriptor;
            try { descriptor = _inference.InferResource(value, language, trace); }
            catch (FormatException exception)
            {
                diagnostics.Add(new SurfaceDiagnostic("FLN222", exception.Message, value.Span));
                continue;
            }
            if (descriptor.Reference is not FileResourceReference file)
            {
                diagnostics.Add(new SurfaceDiagnostic("FLN223",
                    $"LOAD currently accepts local files; '{descriptor.Reference.Kind}' belongs to GET/resource providers.", value.Span));
                continue;
            }
            string variable = command.Alias ?? descriptor.SuggestedVariableName;
            if (command.Alias is not null)
            {
                trace.Add(new InferenceDecision(InferenceKind.VariableName, value.Text, variable,
                    "explicit-AS", value.Span, InferenceConfidence.Explicit));
            }
            if (file.IsPattern)
            {
                if (descriptor.Format != ResourceFormat.Json)
                {
                    diagnostics.Add(new SurfaceDiagnostic("FLN225",
                        $"Glob LOAD currently supports JSON patterns; '{descriptor.Format}' needs a collection codec.", value.Span));
                    continue;
                }
                result.Add(new CommandSyntax([
                    Token("LOADGLOB", PromptTokenKind.Word, command.Span.Start, Math.Min(8, command.Span.Length)),
                    Token($"[{variable}]", PromptTokenKind.Variable, value.Span.Start, value.Span.Length),
                    Token("FROM", PromptTokenKind.Word, value.Span.Start, 0),
                    Token($"{{{file.Path}}}", PromptTokenKind.Reference, value.Span.Start, value.Span.Length)
                ], grammar));
                continue;
            }
            string qualifier = descriptor.Format switch
            {
                ResourceFormat.Json => "CONFIG",
                ResourceFormat.Text => "TEXT",
                _ => string.Empty
            };
            if (qualifier.Length == 0)
            {
                diagnostics.Add(new SurfaceDiagnostic("FLN224",
                    $"LOAD cannot infer a canonical decoder for format '{descriptor.Format}'. Use an explicit canonical command.", value.Span));
                continue;
            }
            result.Add(new CommandSyntax([
                Token("LOAD", PromptTokenKind.Word, command.Span.Start, 4),
                Token(qualifier, PromptTokenKind.Word, value.Span.Start, Math.Min(qualifier.Length, value.Span.Length)),
                Token($"[{variable}]", PromptTokenKind.Variable, value.Span.Start, value.Span.Length),
                Token("FROM", PromptTokenKind.Word, value.Span.Start, 0),
                Token($"{{{file.Path}}}", PromptTokenKind.Reference, value.Span.Start, value.Span.Length)
            ], grammar));
        }
        return result;
    }

    private static CommandSyntax LowerSay(SurfaceCommandSyntax command, PromptGrammar grammar)
    {
        List<PromptToken> tokens = [Token("SAY", PromptTokenKind.Word, command.Span.Start, Math.Min(3, command.Span.Length))];
        foreach (SurfaceValueSyntax value in command.Values)
        {
            string text = SurfacePath(value.Text) ? $"\"{{{value.Text}}}\"" : value.Text;
            tokens.Add(Token(text, Classify(text), value.Span.Start, value.Span.Length));
        }
        return new CommandSyntax(tokens, grammar);
    }

    private static bool SurfacePath(string text)
    {
        if (text.Length >= 2 && (text[0] is '"' or '\'') && text[^1] == text[0]) return false;
        try
        {
            ExpressionSyntax expression = ExpressionSyntaxParser.Parse(text);
            return expression is PropertyExpressionSyntax or IndexExpressionSyntax;
        }
        catch (FormatException) { return false; }
    }

    private static CommandLinkSyntax Link(int predecessor, int successor, CommandLinkKind kind, int position, string text) =>
        new(predecessor, successor, kind, Token(text, PromptTokenKind.Word, position, 0));

    private static PromptToken Token(string text, PromptTokenKind kind, int start, int length) =>
        new(text, kind, Math.Max(0, start), Math.Max(0, length));

    private static PromptTokenKind Classify(string text) =>
        text.Length >= 2 && text[0] == '[' && text[^1] == ']'
            ? PromptTokenKind.Variable
            : text.Length >= 2 && text[0] == '{' && text[^1] == '}'
                ? PromptTokenKind.Reference
                : PromptTokenKind.Word;
}
