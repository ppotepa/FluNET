using FluNET.Prompt;
using FluNET.Prompt.Surface;

namespace FluNET.Compilation.Lowering;

internal static class SurfaceDataLowering
{
    public static bool IsDataStage(SurfaceCommandSyntax command) =>
        command.NormalizedName is "FILTER" or "SORT" or "TAKE" or "SELECT" or "MAP" or "DEFAULT" or "FOREACH" or "GROUP" or "SUM" or "JOIN" or "MATCH";

    public static CommandSyntax? Lower(
        SurfaceCommandSyntax stage,
        string inputVariable,
        string outputVariable,
        PromptGrammar grammar,
        ICollection<SurfaceDiagnostic> diagnostics) => stage.NormalizedName switch
    {
        "FILTER" => Filter(stage, inputVariable, outputVariable, grammar, diagnostics),
        "SORT" => Sort(stage, inputVariable, outputVariable, grammar, diagnostics),
        "TAKE" => Take(stage, inputVariable, outputVariable, grammar, diagnostics),
        "SELECT" => Select(stage, inputVariable, outputVariable, grammar, diagnostics),
        "MAP" => Map(stage, inputVariable, outputVariable, grammar, diagnostics),
        "DEFAULT" => Default(stage, inputVariable, outputVariable, grammar, diagnostics),
        "FOREACH" => ForEach(stage, inputVariable, outputVariable, grammar, diagnostics),
        "GROUP" => Group(stage, inputVariable, outputVariable, grammar, diagnostics),
        "SUM" => Sum(stage, inputVariable, outputVariable, grammar, diagnostics),
        "JOIN" => Join(stage, inputVariable, outputVariable, grammar, diagnostics),
        "MATCH" => Match(stage, inputVariable, outputVariable, grammar, diagnostics),
        _ => null
    };

    private static CommandSyntax? Filter(SurfaceCommandSyntax stage, string input, string output, PromptGrammar grammar, ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (stage.Values.Count != 1 || string.IsNullOrWhiteSpace(stage.Values[0].UnquotedText)) { diagnostics.Add(new SurfaceDiagnostic("FLN260", "FILTER requires one predicate expression.", stage.Span)); return null; }
        return Command("FILTERJSON", output, input, stage.Values[0].UnquotedText, stage.Values[0], grammar);
    }
    private static CommandSyntax? Sort(SurfaceCommandSyntax stage, string input, string output, PromptGrammar grammar, ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (stage.Values.Count != 1) { diagnostics.Add(new SurfaceDiagnostic("FLN261", "SORT requires `BY expression`.", stage.Span)); return null; }
        string text = stage.Values[0].UnquotedText.Trim();
        if (!text.StartsWith("BY ", StringComparison.OrdinalIgnoreCase) || text[3..].Trim().Length == 0) { diagnostics.Add(new SurfaceDiagnostic("FLN261", "SORT requires `BY expression`.", stage.Values[0].Span)); return null; }
        return Command("SORTJSON", output, input, text[3..].Trim(), stage.Values[0], grammar);
    }
    private static CommandSyntax? Take(SurfaceCommandSyntax stage, string input, string output, PromptGrammar grammar, ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (stage.Values.Count != 1 || !int.TryParse(stage.Values[0].UnquotedText, out int count) || count < 0) { diagnostics.Add(new SurfaceDiagnostic("FLN262", "TAKE requires one non-negative integer.", stage.Span)); return null; }
        return Command("TAKEJSON", output, input, count.ToString(System.Globalization.CultureInfo.InvariantCulture), stage.Values[0], grammar);
    }
    private static CommandSyntax? Select(SurfaceCommandSyntax stage, string input, string output, PromptGrammar grammar, ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (stage.Values.Count == 0) { diagnostics.Add(new SurfaceDiagnostic("FLN264", "SELECT requires at least one field or expression.", stage.Span)); return null; }
        return Command("PROJECTJSON", output, input, $"select:{string.Join(", ", stage.Values.Select(value => value.UnquotedText.Trim()))}", stage.Values[0], grammar);
    }
    private static CommandSyntax? Map(SurfaceCommandSyntax stage, string input, string output, PromptGrammar grammar, ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (stage.Values.Count != 1) { diagnostics.Add(new SurfaceDiagnostic("FLN265", "MAP requires `TO { field, alias: expression }`.", stage.Span)); return null; }
        string text = stage.Values[0].UnquotedText.Trim();
        if (!text.StartsWith("TO ", StringComparison.OrdinalIgnoreCase) || text[3..].Trim().Length == 0) { diagnostics.Add(new SurfaceDiagnostic("FLN265", "MAP requires `TO { field, alias: expression }`.", stage.Values[0].Span)); return null; }
        return Command("PROJECTJSON", output, input, $"map:{text[3..].Trim()}", stage.Values[0], grammar);
    }
    private static CommandSyntax? Default(SurfaceCommandSyntax stage, string input, string output, PromptGrammar grammar, ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (stage.Values.Count != 1) { diagnostics.Add(new SurfaceDiagnostic("FLN266", "DEFAULT requires `field TO fallback`.", stage.Span)); return null; }
        string text = stage.Values[0].UnquotedText.Trim();
        int separator = text.IndexOf(" TO ", StringComparison.OrdinalIgnoreCase);
        if (separator <= 0 || separator + 4 >= text.Length) { diagnostics.Add(new SurfaceDiagnostic("FLN266", "DEFAULT requires `field TO fallback`.", stage.Values[0].Span)); return null; }
        return Command("DEFAULTJSON", output, input, $"{text[..separator].Trim()}|{text[(separator + 4)..].Trim()}", stage.Values[0], grammar);
    }
    private static CommandSyntax? ForEach(SurfaceCommandSyntax stage, string input, string output, PromptGrammar grammar, ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (stage.Values.Count != 1) { diagnostics.Add(new SurfaceDiagnostic("FLN275", "FOREACH requires one compiled block descriptor.", stage.Span)); return null; }
        try { _ = SurfaceForEachDescriptor.Decode(stage.Values[0].UnquotedText); }
        catch (FormatException exception) { diagnostics.Add(new SurfaceDiagnostic("FLN275", exception.Message, stage.Span)); return null; }
        return Command("FOREACHJSON", output, input, stage.Values[0].UnquotedText, stage.Values[0], grammar);
    }

    private static CommandSyntax? Group(SurfaceCommandSyntax stage, string input, string output, PromptGrammar grammar, ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (stage.Values.Count != 1) { diagnostics.Add(new SurfaceDiagnostic("FLN280", "GROUP requires `[collection] BY expression` or `BY expression`.", stage.Span)); return null; }
        string text = stage.Values[0].UnquotedText.Trim();
        string actualInput = input;
        string key;
        if (text.StartsWith("BY ", StringComparison.OrdinalIgnoreCase)) key = text[3..].Trim();
        else
        {
            int by = text.IndexOf(" BY ", StringComparison.OrdinalIgnoreCase);
            if (by <= 0) { diagnostics.Add(new SurfaceDiagnostic("FLN280", "GROUP requires `[collection] BY expression` or `BY expression`.", stage.Span)); return null; }
            actualInput = NormalizeVariable(text[..by]);
            key = text[(by + 4)..].Trim();
        }
        if (key.Length == 0) { diagnostics.Add(new SurfaceDiagnostic("FLN280", "GROUP key cannot be empty.", stage.Span)); return null; }
        return Command("GROUPJSON", output, actualInput, key, stage.Values[0], grammar);
    }

    private static CommandSyntax? Sum(SurfaceCommandSyntax stage, string input, string output, PromptGrammar grammar, ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (stage.Values.Count != 1 || string.IsNullOrWhiteSpace(stage.Values[0].UnquotedText)) { diagnostics.Add(new SurfaceDiagnostic("FLN281", "SUM requires one numeric expression.", stage.Span)); return null; }
        return Command("SUMJSON", output, input, stage.Values[0].UnquotedText.Trim(), stage.Values[0], grammar);
    }

    private static CommandSyntax? Join(SurfaceCommandSyntax stage, string input, string output, PromptGrammar grammar, ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (stage.Values.Count != 1) { diagnostics.Add(new SurfaceDiagnostic("FLN282", "JOIN requires `left WITH right ON left.key = right.key`.", stage.Span)); return null; }
        string text = stage.Values[0].UnquotedText.Trim();
        int with = text.IndexOf(" WITH ", StringComparison.OrdinalIgnoreCase);
        int on = text.IndexOf(" ON ", StringComparison.OrdinalIgnoreCase);
        if (with <= 0 || on <= with + 6) { diagnostics.Add(new SurfaceDiagnostic("FLN282", "JOIN requires `left WITH right ON left.key = right.key`.", stage.Span)); return null; }
        string left = NormalizeVariable(text[..with]);
        string right = NormalizeVariable(text[(with + 6)..on]);
        string condition = text[(on + 4)..].Trim();
        if (!TryMatchKeys(condition, left, right, out string? leftKey, out string? rightKey)) { diagnostics.Add(new SurfaceDiagnostic("FLN282", "JOIN ON must compare a left and right key with = or ==.", stage.Span)); return null; }
        return JoinCommand(output, left, right, leftKey!, rightKey!, stage.Values[0], grammar);
    }

    private static CommandSyntax? Match(SurfaceCommandSyntax stage, string input, string output, PromptGrammar grammar, ICollection<SurfaceDiagnostic> diagnostics)
    {
        if (stage.Values.Count != 1) { diagnostics.Add(new SurfaceDiagnostic("FLN283", "MATCH requires `left.key TO right.key`.", stage.Span)); return null; }
        string text = stage.Values[0].UnquotedText.Trim();
        int to = text.IndexOf(" TO ", StringComparison.OrdinalIgnoreCase);
        if (to <= 0) { diagnostics.Add(new SurfaceDiagnostic("FLN283", "MATCH requires `left.key TO right.key`.", stage.Span)); return null; }
        string leftPath = text[..to].Trim();
        string rightPath = text[(to + 4)..].Trim();
        if (!SplitRoot(leftPath, out string? left, out string? leftKey) || !SplitRoot(rightPath, out string? right, out string? rightKey))
        { diagnostics.Add(new SurfaceDiagnostic("FLN283", "MATCH paths must be `collection.field`.", stage.Span)); return null; }
        return JoinCommand(output, left!, right!, leftKey!, rightKey!, stage.Values[0], grammar);
    }

    private static bool TryMatchKeys(string condition, string left, string right, out string? leftKey, out string? rightKey)
    {
        leftKey = rightKey = null;
        int equal = condition.IndexOf("==", StringComparison.Ordinal);
        int width = 2;
        if (equal < 0) { equal = condition.IndexOf('='); width = 1; }
        if (equal <= 0) return false;
        string a = condition[..equal].Trim();
        string b = condition[(equal + width)..].Trim();
        if (!StripRoot(a, left, out leftKey) || !StripRoot(b, right, out rightKey))
        {
            if (!StripRoot(b, left, out leftKey) || !StripRoot(a, right, out rightKey)) return false;
        }
        return true;
    }

    private static bool SplitRoot(string path, out string? root, out string? key)
    {
        int dot = path.IndexOf('.');
        if (dot <= 0 || dot == path.Length - 1) { root = key = null; return false; }
        root = NormalizeVariable(path[..dot]);
        key = path[(dot + 1)..].Trim();
        return root.Length > 0 && key.Length > 0;
    }
    private static bool StripRoot(string path, string root, out string? key)
    {
        string prefix = root + ".";
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { key = null; return false; }
        key = path[prefix.Length..].Trim();
        return key.Length > 0;
    }
    private static string NormalizeVariable(string value) => value.Trim().TrimStart('[').TrimEnd(']');

    private static CommandSyntax JoinCommand(string output, string left, string right, string leftKey, string rightKey, SurfaceValueSyntax source, PromptGrammar grammar) =>
        new([
            Token("JOINJSON", PromptTokenKind.Word, source.Span.Start, 0),
            Token($"[{output}]", PromptTokenKind.Variable, source.Span.Start, 0),
            Token("FROM", PromptTokenKind.Word, source.Span.Start, 0),
            Token($"[{left}]", PromptTokenKind.Variable, source.Span.Start, 0),
            Token("TO", PromptTokenKind.Word, source.Span.Start, 0),
            Token($"[{right}]", PromptTokenKind.Variable, source.Span.Start, 0),
            Token("USING", PromptTokenKind.Word, source.Span.Start, 0),
            Token($"{{{leftKey}|{rightKey}}}", PromptTokenKind.Reference, source.Span.Start, source.Span.Length)
        ], grammar);

    private static CommandSyntax Command(string verb, string output, string input, string value, SurfaceValueSyntax source, PromptGrammar grammar) =>
        new([
            Token(verb, PromptTokenKind.Word, source.Span.Start, 0),
            Token($"[{output}]", PromptTokenKind.Variable, source.Span.Start, 0),
            Token("FROM", PromptTokenKind.Word, source.Span.Start, 0),
            Token($"[{input}]", PromptTokenKind.Variable, source.Span.Start, 0),
            Token("USING", PromptTokenKind.Word, source.Span.Start, 0),
            Token($"{{{value}}}", PromptTokenKind.Reference, source.Span.Start, source.Span.Length)
        ], grammar);

    private static PromptToken Token(string text, PromptTokenKind kind, int start, int length) => new(text, kind, Math.Max(0, start), Math.Max(0, length));
}
