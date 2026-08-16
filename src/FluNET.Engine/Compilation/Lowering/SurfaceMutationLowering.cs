using FluNET.Prompt;
using FluNET.Prompt.Surface;

namespace FluNET.Compilation.Lowering;

internal static class SurfaceMutationLowering
{
    public static CommandSyntax? Post(SurfaceCommandSyntax command, PromptGrammar grammar, ICollection<SurfaceDiagnostic> diagnostics, string? authenticationSecret = null)
    {
        if (!TryValueToTarget(command, "POST", diagnostics, out string? value, out string? target)) return null;
        if (!Uri.TryCreate(target, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https"))
        { diagnostics.Add(new SurfaceDiagnostic("FLN306", "POST target must be an absolute HTTP(S) URI.", command.Span)); return null; }
        string theme = Variable(value!);
        List<PromptToken> tokens = [
            Token("POST", PromptTokenKind.Word, command.Span.Start), Token("JSON", PromptTokenKind.Word, command.Span.Start),
            Token(theme, theme.StartsWith('[') ? PromptTokenKind.Variable : PromptTokenKind.Word, command.Span.Start),
            Token("TO", PromptTokenKind.Word, command.Span.Start), Token($"{{{uri}}}", PromptTokenKind.Reference, command.Span.Start)
        ];
        AddCredential(tokens, authenticationSecret, command.Span.Start);
        return new CommandSyntax(tokens, grammar);
    }

    public static CommandSyntax? Save(SurfaceCommandSyntax command, PromptGrammar grammar, ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (!TryValueToTarget(command, "SAVE", diagnostics, out string? value, out string? target)) return null;
        string theme = Variable(value!);
        string normalizedTarget = target!.Trim().Trim('"', '\'');
        if (value!.StartsWith("CSV ", StringComparison.OrdinalIgnoreCase))
        {
            theme = Variable(value[4..].Trim());
            return new CommandSyntax([
                Token("SAVECSV", PromptTokenKind.Word, command.Span.Start),
                Token(theme, theme.StartsWith('[') ? PromptTokenKind.Variable : PromptTokenKind.Word, command.Span.Start),
                Token("TO", PromptTokenKind.Word, command.Span.Start),
                FileTarget(normalizedTarget, command.Span.Start)
            ], grammar);
        }
        bool explicitJson = value.StartsWith("JSON ", StringComparison.OrdinalIgnoreCase);
        if (explicitJson)
            theme = Variable(value[5..].Trim());
        if (normalizedTarget.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
        {
            string key = normalizedTarget["blob:".Length..].Trim();
            if (key.Length == 0)
            {
                diagnostics.Add(new SurfaceDiagnostic("FLN342", "Blob target requires a non-empty key.", command.Span));
                return null;
            }

            return new CommandSyntax([
                Token("PUTBLOB", PromptTokenKind.Word, command.Span.Start),
                Token($"[{command.Alias ?? "saved"}]", PromptTokenKind.Variable, command.Span.Start),
                Token("FROM", PromptTokenKind.Word, command.Span.Start),
                Token($"{{{key}}}", PromptTokenKind.Reference, command.Span.Start),
                Token("USING", PromptTokenKind.Word, command.Span.Start),
                Token(theme, theme.StartsWith('[') ? PromptTokenKind.Variable : PromptTokenKind.Word, command.Span.Start)
            ], grammar);
        }

        bool json = explicitJson || command.NormalizedName == "SAVE_TO" &&
            string.Equals(Path.GetExtension(normalizedTarget), ".json", StringComparison.OrdinalIgnoreCase);
        return new CommandSyntax([
            Token(json ? "SAVEJSON" : "SAVE", PromptTokenKind.Word, command.Span.Start),
            Token(theme, theme.StartsWith('[') ? PromptTokenKind.Variable : PromptTokenKind.Word, command.Span.Start),
            Token("TO", PromptTokenKind.Word, command.Span.Start),
            FileTarget(normalizedTarget, command.Span.Start)
        ], grammar);
    }

    public static CommandSyntax? HttpJson(
        SurfaceCommandSyntax command,
        string verb,
        PromptGrammar grammar,
        ICollection<SurfaceDiagnostic> diagnostics,
        string? authenticationSecret = null)
    {
        if (!TryValueToTarget(command, verb, diagnostics, out string? value, out string? target)) return null;
        if (!Uri.TryCreate(target, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https"))
        {
            diagnostics.Add(new SurfaceDiagnostic("FLN345", $"{verb} target must be an absolute HTTP(S) URI.", command.Span));
            return null;
        }

        string theme = Variable(value!);
        List<PromptToken> tokens = [
            Token(verb + "JSON", PromptTokenKind.Word, command.Span.Start),
            Token(theme, theme.StartsWith('[') ? PromptTokenKind.Variable : PromptTokenKind.Word, command.Span.Start),
            Token("TO", PromptTokenKind.Word, command.Span.Start),
            Token($"{{{uri}}}", PromptTokenKind.Reference, command.Span.Start)
        ];
        AddCredential(tokens, authenticationSecret, command.Span.Start);
        return new CommandSyntax(tokens, grammar);
    }

    public static CommandSyntax? Emit(
        SurfaceCommandSyntax command,
        PromptGrammar grammar,
        ICollection<SurfaceDiagnostic> diagnostics,
        string? authenticationSecret = null)
    {
        if (!TryValueToTarget(command, "EMIT", diagnostics, out string? value, out string? target)) return null;
        if (!Uri.TryCreate(target, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https"))
        {
            diagnostics.Add(new SurfaceDiagnostic("FLN360", "EMIT target must be an absolute HTTP(S) URI.", command.Span));
            return null;
        }

        string payload = Variable(value!);
        List<PromptToken> tokens = [
            Token("EMITEVENT", PromptTokenKind.Word, command.Span.Start),
            Token(payload, payload.StartsWith('[') ? PromptTokenKind.Variable : PromptTokenKind.Word, command.Span.Start),
            Token("TO", PromptTokenKind.Word, command.Span.Start),
            Token($"{{{uri}}}", PromptTokenKind.Reference, command.Span.Start)
        ];
        AddCredential(tokens, authenticationSecret, command.Span.Start);
        return new CommandSyntax(tokens, grammar);
    }

    public static CommandSyntax? Publish(SurfaceCommandSyntax command, PromptGrammar grammar, ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (!TryValueToTarget(command, "PUBLISH", diagnostics, out string? value, out string? target)) return null;
        string payload = value!.Trim();
        if (payload.Length > 0 && payload[0] == '[')
            payload = Variable(payload);
        else
            payload = payload.Trim('"', '\'');
        PromptToken topic = target!.Length >= 2 && target[0] == '[' && target[^1] == ']'
            ? Token(target, PromptTokenKind.Variable, command.Span.Start)
            : Token(target.Trim().Trim('"', '\''), PromptTokenKind.Word, command.Span.Start);
        return new CommandSyntax([
            Token("PUBLISHMESSAGE", PromptTokenKind.Word, command.Span.Start),
            Token(payload, payload.StartsWith('[') ? PromptTokenKind.Variable : PromptTokenKind.Word, command.Span.Start),
            Token("TO", PromptTokenKind.Word, command.Span.Start),
            topic
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
    private static PromptToken FileTarget(string value, int start) =>
        value.Length >= 2 && value[0] == '[' && value[^1] == ']'
            ? Token(value, PromptTokenKind.Variable, start)
            : Token($"{{{value}}}", PromptTokenKind.Reference, start);
    private static void AddCredential(List<PromptToken> tokens, string? secret, int start)
    {
        if (string.IsNullOrWhiteSpace(secret)) return;
        tokens.Add(Token("USING", PromptTokenKind.Word, start));
        tokens.Add(Token($"{{{secret}}}", PromptTokenKind.Reference, start));
    }
    private static PromptToken Token(string text, PromptTokenKind kind, int start) => new(text, kind, Math.Max(0, start), 0);
}
