using FluNET.Prompt;
using FluNET.Prompt.Surface;

namespace FluNET.Compilation.Lowering;

internal static class SurfaceMutationLowering
{
    public static CommandSyntax? Post(SurfaceCommandSyntax command, PromptGrammar grammar, ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (command.Values.Count != 1)
        { diagnostics.Add(new SurfaceDiagnostic("FLN305", "POST requires `value TO absolute-uri`.", command.Span)); return null; }
        string text = command.Values[0].UnquotedText.Trim(); int to = text.IndexOf(" TO ", StringComparison.OrdinalIgnoreCase);
        if (to <= 0 || to + 4 >= text.Length)
        { diagnostics.Add(new SurfaceDiagnostic("FLN305", "POST requires `value TO absolute-uri`.", command.Span)); return null; }
        string value = text[..to].Trim(); string target = text[(to + 4)..].Trim();
        if (!Uri.TryCreate(target, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https"))
        { diagnostics.Add(new SurfaceDiagnostic("FLN306", "POST target must be an absolute HTTP(S) URI in this batch.", command.Span)); return null; }
        string theme = IsIdentifier(value) ? $"[{value}]" : value;
        return new CommandSyntax([
            Token("POST", PromptTokenKind.Word, command.Span.Start), Token("JSON", PromptTokenKind.Word, command.Span.Start),
            Token(theme, theme.StartsWith('[') ? PromptTokenKind.Variable : PromptTokenKind.Word, command.Span.Start),
            Token("TO", PromptTokenKind.Word, command.Span.Start), Token($"{{{uri}}}", PromptTokenKind.Reference, command.Span.Start)
        ], grammar);
    }
    private static bool IsIdentifier(string value) => value.Length > 0 && (char.IsLetter(value[0]) || value[0] == '_') && value.Skip(1).All(ch => char.IsLetterOrDigit(ch) || ch == '_');
    private static PromptToken Token(string text, PromptTokenKind kind, int start) => new(text, kind, Math.Max(0, start), 0);
}
