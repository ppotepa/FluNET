using FluNET.Prompt;
using FluNET.Prompt.Surface;

namespace FluNET.Compilation.Lowering;

internal static class SurfaceMutationLowering
{
    public static CommandSyntax? Post(SurfaceCommandSyntax command, PromptGrammar grammar, ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (!TryValueToTarget(command, "POST", diagnostics, out string? value, out string? target)) return null;
        if (!Uri.TryCreate(target, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https"))
        { diagnostics.Add(new SurfaceDiagnostic("FLN306", "POST target must be an absolute HTTP(S) URI.", command.Span)); return null; }
        string theme = Variable(value!);
        return new CommandSyntax([
            Token("POST", PromptTokenKind.Word, command.Span.Start), Token("JSON", PromptTokenKind.Word, command.Span.Start),
            Token(theme, theme.StartsWith('[') ? PromptTokenKind.Variable : PromptTokenKind.Word, command.Span.Start),
            Token("TO", PromptTokenKind.Word, command.Span.Start), Token($"{{{uri}}}", PromptTokenKind.Reference, command.Span.Start)
        ], grammar);
    }

    public static CommandSyntax? Save(SurfaceCommandSyntax command, PromptGrammar grammar, ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (!TryValueToTarget(command, "SAVE", diagnostics, out string? value, out string? target)) return null;
        string theme = Variable(value!);
        return new CommandSyntax([
            Token("SAVE", PromptTokenKind.Word, command.Span.Start),
            Token(theme, theme.StartsWith('[') ? PromptTokenKind.Variable : PromptTokenKind.Word, command.Span.Start),
            Token("TO", PromptTokenKind.Word, command.Span.Start),
            Token($"{{{target}}}", PromptTokenKind.Reference, command.Span.Start)
        ], grammar);
    }

    private static bool TryValueToTarget(SurfaceCommandSyntax command, string name, ICollection<SurfaceDiagnostic> diagnostics, out string? value, out string? target)
    {
        value = target = null;
        if (command.Values.Count != 1) { diagnostics.Add(new SurfaceDiagnostic("FLN305", $"{name} requires `value TO target`.", command.Span)); return false; }
        string text = command.Values[0].UnquotedText.Trim();
        int to = text.IndexOf(" TO ", StringComparison.OrdinalIgnoreCase);
        if (to <= 0 || to + 4 >= text.Length) { diagnostics.Add(new SurfaceDiagnostic("FLN305", $"{name} requires `value TO target`.", command.Span)); return false; }
        value = text[..to].Trim(); target = text[(to + 4)..].Trim(); return true;
    }

    private static string Variable(string value) =>
        value.Length >= 2 && value[0] == '[' && value[^1] == ']' ? value :
        value.Length > 0 && (char.IsLetter(value[0]) || value[0] == '_') && value.Skip(1).All(ch => char.IsLetterOrDigit(ch) || ch == '_') ? $"[{value}]" : value;
    private static PromptToken Token(string text, PromptTokenKind kind, int start) => new(text, kind, Math.Max(0, start), 0);
}
