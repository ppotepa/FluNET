using FluNET.Compilation.Inference;
using FluNET.Prompt;
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

/// <summary>
/// Lowers compact surface syntax directly to canonical PromptSyntax. It never
/// emits a prompt string and never invokes ProcessedPrompt as a second parser.
/// </summary>
public sealed class SurfaceLowerer
{
    public LoweringResult Lower(SurfaceParseResult parse, PromptGrammar grammar)
    {
        ArgumentNullException.ThrowIfNull(parse);
        ArgumentNullException.ThrowIfNull(grammar);
        List<CommandSyntax> commands = [];
        List<SourceMapEntry> map = [];
        List<SurfaceDiagnostic> diagnostics = [.. parse.Diagnostics];
        InferenceTrace trace = new();

        for (int index = 0; index < parse.Program.Statements.Count; index++)
        {
            if (parse.Program.Statements[index] is not SurfaceCommandSyntax command)
            {
                diagnostics.Add(new SurfaceDiagnostic(
                    "FLN210",
                    $"Unsupported surface statement '{parse.Program.Statements[index].GetType().Name}'.",
                    parse.Program.Statements[index].Span));
                continue;
            }

            CommandSyntax? lowered = command.NormalizedName switch
            {
                "SAY" => LowerSay(command, grammar),
                _ => null
            };
            if (lowered is null)
            {
                diagnostics.Add(new SurfaceDiagnostic(
                    "FLN211",
                    $"Surface command '{command.Name}' does not have a lowering rule yet.",
                    command.Span));
                continue;
            }

            commands.Add(lowered);
            map.Add(new SourceMapEntry(commands.Count - 1, "command", command.Span));
        }

        return new LoweringResult(
            parse.Document,
            parse.Program,
            new PromptSyntax(commands),
            new SourceMap(map),
            trace,
            diagnostics);
    }

    private static CommandSyntax LowerSay(SurfaceCommandSyntax command, PromptGrammar grammar)
    {
        List<PromptToken> tokens =
        [
            new PromptToken("SAY", PromptTokenKind.Word, command.Span.Start, Math.Min(3, command.Span.Length))
        ];
        foreach (SurfaceValueSyntax value in command.Values)
        {
            tokens.Add(new PromptToken(
                value.Text,
                Classify(value.Text),
                value.Span.Start,
                value.Span.Length));
        }
        return new CommandSyntax(tokens, grammar);
    }

    private static PromptTokenKind Classify(string text) =>
        text.Length >= 2 && text[0] == '[' && text[^1] == ']'
            ? PromptTokenKind.Variable
            : text.Length >= 2 && text[0] == '{' && text[^1] == '}'
                ? PromptTokenKind.Reference
                : PromptTokenKind.Word;
}
