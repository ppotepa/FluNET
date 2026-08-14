using FluNET.Prompt;
using FluNET.Prompt.Surface;

namespace FluNET.Compilation.Lowering;

internal static class SurfaceDataLowering
{
    public static bool IsDataStage(SurfaceCommandSyntax command) =>
        command.NormalizedName is "FILTER" or "SORT" or "TAKE" or "SELECT" or "MAP" or "DEFAULT" or "FOREACH";

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
        string projection = string.Join(", ", stage.Values.Select(value => value.UnquotedText.Trim()));
        return Command("PROJECTJSON", output, input, $"select:{projection}", stage.Values[0], grammar);
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
        if (stage.Values.Count != 1)
        {
            diagnostics.Add(new SurfaceDiagnostic("FLN275", "FOREACH requires one compiled block descriptor.", stage.Span));
            return null;
        }
        try { _ = SurfaceForEachDescriptor.Decode(stage.Values[0].UnquotedText); }
        catch (FormatException exception) { diagnostics.Add(new SurfaceDiagnostic("FLN275", exception.Message, stage.Span)); return null; }
        return Command("FOREACHJSON", output, input, stage.Values[0].UnquotedText, stage.Values[0], grammar);
    }

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
