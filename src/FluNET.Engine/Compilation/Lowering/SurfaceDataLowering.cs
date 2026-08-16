using FluNET.Prompt;
using FluNET.Prompt.Surface;

namespace FluNET.Compilation.Lowering;

internal static class SurfaceDataLowering
{
    public static bool IsDataStage(SurfaceCommandSyntax command) => command.NormalizedName is
        "FILTER" or "SORT" or "TAKE" or "SKIP" or "DISTINCT" or "SELECT" or "MAP" or
        "DEFAULT" or "FOREACH" or "GROUP" or "SUM" or "COUNT" or "AVG" or "MIN" or "MAX" or "JOIN" or "MATCH";

    public static bool HasExplicitInput(SurfaceCommandSyntax command)
    {
        if (command.NormalizedName is "JOIN" or "MATCH") return true;
        if (command.Values.Count == 1)
        {
            string value = command.Values[0].UnquotedText.Trim();
            if (command.NormalizedName is "FILTER" && value.Contains(" WHERE ", StringComparison.OrdinalIgnoreCase)) return true;
            if (command.NormalizedName is "SORT" && value.Contains(" BY ", StringComparison.OrdinalIgnoreCase)) return true;
            if (command.NormalizedName is "COUNT" && IsIdentifier(value.Trim('[', ']'))) return true;
        }
        if (command.NormalizedName != "FOREACH" || command.Values.Count != 1) return false;
        try { return SurfaceForEachDescriptor.Decode(command.Values[0].UnquotedText).SourceName is not null; }
        catch { return false; }
    }

    public static CommandSyntax? Lower(
        SurfaceCommandSyntax syntax,
        string input,
        string output,
        PromptGrammar grammar,
        ICollection<SurfaceDiagnostic> diagnostics) => syntax.NormalizedName switch
    {
        "FILTER" => Filter(syntax, input, output, grammar, diagnostics),
        "SORT" => Sort(syntax, input, output, grammar, diagnostics),
        "TAKE" => Take(syntax, input, output, grammar, diagnostics),
        "SKIP" => Skip(syntax, input, output, grammar, diagnostics),
        "DISTINCT" => Distinct(syntax, input, output, grammar, diagnostics),
        "SELECT" => Select(syntax, input, output, grammar, diagnostics),
        "MAP" => Map(syntax, input, output, grammar, diagnostics),
        "DEFAULT" => Default(syntax, input, output, grammar, diagnostics),
        "FOREACH" => ForEach(syntax, input, output, grammar, diagnostics),
        "GROUP" => Group(syntax, input, output, grammar, diagnostics),
        "SUM" => Sum(syntax, input, output, grammar, diagnostics),
        "COUNT" => Aggregate(syntax, "COUNTJSON", input, output, grammar, diagnostics),
        "AVG" => Aggregate(syntax, "AVGJSON", input, output, grammar, diagnostics),
        "MIN" => Aggregate(syntax, "MINJSON", input, output, grammar, diagnostics),
        "MAX" => Aggregate(syntax, "MAXJSON", input, output, grammar, diagnostics),
        "JOIN" => Join(syntax, input, output, grammar, diagnostics),
        "MATCH" => Match(syntax, input, output, grammar, diagnostics),
        _ => null
    };

    private static CommandSyntax? Filter(SurfaceCommandSyntax s, string i, string o, PromptGrammar g, ICollection<SurfaceDiagnostic> d)
    {
        if (s.Values.Count != 1 || string.IsNullOrWhiteSpace(s.Values[0].UnquotedText))
            return Error(d, "FLN260", "FILTER requires one predicate expression.", s);
        (string actual, string predicate) = ExplicitInput(s, i, " WHERE ");
        return Cmd("FILTERJSON", o, actual, predicate, s.Values[0], g);
    }

    private static CommandSyntax? Sort(SurfaceCommandSyntax s, string i, string o, PromptGrammar g, ICollection<SurfaceDiagnostic> d)
    {
        string value = s.Values.Count == 1 ? s.Values[0].UnquotedText.Trim() : string.Empty;
        if (s.Values.Count != 1 ||
            !value.StartsWith("BY ", StringComparison.OrdinalIgnoreCase) &&
            !value.Contains(" BY ", StringComparison.OrdinalIgnoreCase))
            return Error(d, "FLN261", "SORT requires `BY expression`.", s);
        (string actual, string key) = ExplicitInput(s, i, " BY ");
        if (value.StartsWith("BY ", StringComparison.OrdinalIgnoreCase)) key = value[3..].Trim();
        return Cmd("SORTJSON", o, actual, key, s.Values[0], g);
    }

    private static CommandSyntax? Take(SurfaceCommandSyntax s, string i, string o, PromptGrammar g, ICollection<SurfaceDiagnostic> d) =>
        Count(s, "TAKE", "TAKEJSON", "FLN262", i, o, g, d);

    private static CommandSyntax? Skip(SurfaceCommandSyntax s, string i, string o, PromptGrammar g, ICollection<SurfaceDiagnostic> d) =>
        Count(s, "SKIP", "SKIPJSON", "FLN267", i, o, g, d);

    private static CommandSyntax? Count(SurfaceCommandSyntax s, string name, string verb, string code, string i, string o, PromptGrammar g, ICollection<SurfaceDiagnostic> d)
    {
        if (s.Values.Count != 1 || !int.TryParse(s.Values[0].UnquotedText.Trim(), out int count) || count < 0)
            return Error(d, code, $"{name} requires one non-negative integer.", s);
        return Cmd(verb, o, i, count.ToString(), s.Values[0], g);
    }

    private static CommandSyntax? Distinct(SurfaceCommandSyntax s, string i, string o, PromptGrammar g, ICollection<SurfaceDiagnostic> d)
    {
        if (s.Values.Count > 1 || (s.Values.Count == 1 &&
            !s.Values[0].UnquotedText.Trim().StartsWith("BY ", StringComparison.OrdinalIgnoreCase)))
            return Error(d, "FLN268", "DISTINCT accepts optional `BY expression`.", s);
        string descriptor = s.Values.Count == 0 ? string.Empty : s.Values[0].UnquotedText.Trim()[3..].Trim();
        return Cmd("DISTINCTJSON", o, i, descriptor, s.Values.FirstOrDefault() ?? new SurfaceValueSyntax(string.Empty, s.Span), g);
    }

    private static CommandSyntax? Select(SurfaceCommandSyntax s, string i, string o, PromptGrammar g, ICollection<SurfaceDiagnostic> d)
    {
        if (s.Values.Count == 0) return Error(d, "FLN264", "SELECT requires fields.", s);
        return Cmd("PROJECTJSON", o, i, $"select:{string.Join(", ", s.Values.Select(v => v.UnquotedText.Trim()))}", s.Values[0], g);
    }

    private static CommandSyntax? Map(SurfaceCommandSyntax s, string i, string o, PromptGrammar g, ICollection<SurfaceDiagnostic> d)
    {
        if (s.Values.Count != 1 || !s.Values[0].UnquotedText.Trim().StartsWith("TO ", StringComparison.OrdinalIgnoreCase))
            return Error(d, "FLN265", "MAP requires `TO {...}`.", s);
        return Cmd("PROJECTJSON", o, i, $"map:{s.Values[0].UnquotedText.Trim()[3..].Trim()}", s.Values[0], g);
    }

    private static CommandSyntax? Default(SurfaceCommandSyntax s, string i, string o, PromptGrammar g, ICollection<SurfaceDiagnostic> d)
    {
        if (s.Values.Count != 1) return Error(d, "FLN266", "DEFAULT requires `field TO fallback`.", s);
        string t = s.Values[0].UnquotedText;
        int x = t.IndexOf(" TO ", StringComparison.OrdinalIgnoreCase);
        if (x <= 0) return Error(d, "FLN266", "DEFAULT requires `field TO fallback`.", s);
        return Cmd("DEFAULTJSON", o, i, $"{t[..x].Trim()}|{t[(x + 4)..].Trim()}", s.Values[0], g);
    }

    private static CommandSyntax? ForEach(SurfaceCommandSyntax s, string input, string o, PromptGrammar g, ICollection<SurfaceDiagnostic> d)
    {
        if (s.Values.Count != 1) return Error(d, "FLN275", "FOREACH requires descriptor.", s);
        SurfaceForEachDescriptor descriptor;
        try { descriptor = SurfaceForEachDescriptor.Decode(s.Values[0].UnquotedText); }
        catch (FormatException e) { return Error(d, "FLN275", e.Message, s); }
        string actual = descriptor.SourceName ?? input;
        if (string.IsNullOrWhiteSpace(actual)) return Error(d, "FLN263", "FOR EACH requires a previous data value or explicit IN collection.", s);
        return Cmd("FOREACHJSON", o, Norm(actual), s.Values[0].UnquotedText, s.Values[0], g);
    }

    private static CommandSyntax? Group(SurfaceCommandSyntax s, string i, string o, PromptGrammar g, ICollection<SurfaceDiagnostic> d)
    {
        if (s.Values.Count != 1) return Error(d, "FLN280", "GROUP requires BY expression.", s);
        string t = s.Values[0].UnquotedText.Trim(), actual = i, key;
        if (t.StartsWith("BY ", StringComparison.OrdinalIgnoreCase)) key = t[3..].Trim();
        else
        {
            int by = t.IndexOf(" BY ", StringComparison.OrdinalIgnoreCase);
            if (by <= 0) return Error(d, "FLN280", "GROUP requires BY expression.", s);
            actual = Norm(t[..by]); key = t[(by + 4)..].Trim();
        }
        return Cmd("GROUPJSON", o, actual, key, s.Values[0], g);
    }

    private static CommandSyntax? Sum(SurfaceCommandSyntax s, string i, string o, PromptGrammar g, ICollection<SurfaceDiagnostic> d)
    {
        if (s.Values.Count != 1) return Error(d, "FLN281", "SUM requires expression.", s);
        return Cmd("SUMJSON", o, i, s.Values[0].UnquotedText.Trim(), s.Values[0], g);
    }

    private static CommandSyntax? Aggregate(SurfaceCommandSyntax s, string verb, string i, string o, PromptGrammar g, ICollection<SurfaceDiagnostic> d)
    {
        if (s.Values.Count > 1) return Error(d, "FLN284", $"{s.Name} accepts at most one expression.", s);
        SurfaceValueSyntax source = s.Values.FirstOrDefault() ?? new SurfaceValueSyntax(string.Empty, s.Span);
        string value = source.UnquotedText.Trim();
        if ((verb == "COUNTJSON" && IsIdentifier(value.Trim('[', ']'))) ||
            value.StartsWith('[') && value.EndsWith(']') && value.Length > 2)
            return Cmd(verb, o, Norm(value), string.Empty, source, g);
        return Cmd(verb, o, i, value, source, g);
    }

    private static (string Input, string Expression) ExplicitInput(
        SurfaceCommandSyntax syntax,
        string fallback,
        string marker)
    {
        string value = syntax.Values[0].UnquotedText.Trim();
        int separator = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (separator <= 0)
            return (fallback, value);

        string input = Norm(value[..separator].Trim());
        string expression = value[(separator + marker.Length)..].Trim();
        return (input.Length == 0 ? fallback : input, expression);
    }

    private static bool IsIdentifier(string value) =>
        value.Length > 0 &&
        (char.IsLetter(value[0]) || value[0] == '_') &&
        value.Skip(1).All(character => char.IsLetterOrDigit(character) || character == '_');

    private static CommandSyntax? Join(SurfaceCommandSyntax s, string i, string o, PromptGrammar g, ICollection<SurfaceDiagnostic> d)
    {
        if (s.Values.Count != 1) return Error(d, "FLN282", "JOIN requires left WITH right ON left.key = right.key.", s);
        string t = s.Values[0].UnquotedText;
        int w = t.IndexOf(" WITH ", StringComparison.OrdinalIgnoreCase), on = t.IndexOf(" ON ", StringComparison.OrdinalIgnoreCase);
        if (w <= 0 || on <= w + 6) return Error(d, "FLN282", "Invalid JOIN.", s);
        string l = Norm(t[..w]), r = Norm(t[(w + 6)..on]), condition = t[(on + 4)..];
        if (!Keys(condition, l, r, out string? lk, out string? rk)) return Error(d, "FLN282", "Invalid JOIN keys.", s);
        return JoinCmd(o, l, r, lk!, rk!, s.Values[0], g);
    }

    private static CommandSyntax? Match(SurfaceCommandSyntax s, string i, string o, PromptGrammar g, ICollection<SurfaceDiagnostic> d)
    {
        if (s.Values.Count != 1) return Error(d, "FLN283", "MATCH requires left.key TO right.key.", s);
        string t = s.Values[0].UnquotedText; int to = t.IndexOf(" TO ", StringComparison.OrdinalIgnoreCase);
        if (to <= 0 || !Root(t[..to], out string? l, out string? lk) || !Root(t[(to + 4)..], out string? r, out string? rk))
            return Error(d, "FLN283", "Invalid MATCH.", s);
        return JoinCmd(o, l!, r!, lk!, rk!, s.Values[0], g);
    }

    private static bool Keys(string c, string l, string r, out string? lk, out string? rk)
    {
        lk = rk = null; int e = c.IndexOf("==", StringComparison.Ordinal), width = 2;
        if (e < 0) { e = c.IndexOf('='); width = 1; }
        if (e <= 0) return false;
        string a = c[..e].Trim(), b = c[(e + width)..].Trim();
        return Strip(a, l, out lk) && Strip(b, r, out rk) || Strip(b, l, out lk) && Strip(a, r, out rk);
    }

    private static bool Root(string p, out string? root, out string? key)
    {
        int dot = p.IndexOf('.');
        if (dot <= 0) { root = key = null; return false; }
        root = Norm(p[..dot]); key = p[(dot + 1)..].Trim(); return key.Length > 0;
    }

    private static bool Strip(string p, string root, out string? key)
    {
        string prefix = root + ".";
        if (!p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { key = null; return false; }
        key = p[prefix.Length..].Trim(); return key.Length > 0;
    }

    private static string Norm(string value) => value.Trim().TrimStart('[').TrimEnd(']');

    private static CommandSyntax JoinCmd(string o, string l, string r, string lk, string rk, SurfaceValueSyntax s, PromptGrammar g) => new([
        T("JOINJSON", PromptTokenKind.Word, s.Span.Start), T($"[{o}]", PromptTokenKind.Variable, s.Span.Start),
        T("FROM", PromptTokenKind.Word, s.Span.Start), T($"[{l}]", PromptTokenKind.Variable, s.Span.Start),
        T("TO", PromptTokenKind.Word, s.Span.Start), T($"[{r}]", PromptTokenKind.Variable, s.Span.Start),
        T("USING", PromptTokenKind.Word, s.Span.Start), T($"{{{lk}|{rk}}}", PromptTokenKind.Reference, s.Span.Start)], g);

    private static CommandSyntax Cmd(string verb, string output, string input, string value, SurfaceValueSyntax source, PromptGrammar grammar)
    {
        List<PromptToken> tokens = [
            T(verb, PromptTokenKind.Word, source.Span.Start),
            T($"[{output}]", PromptTokenKind.Variable, source.Span.Start),
            T("FROM", PromptTokenKind.Word, source.Span.Start),
            T($"[{input}]", PromptTokenKind.Variable, source.Span.Start)];
        if (value.Length > 0) tokens.AddRange([T("USING", PromptTokenKind.Word, source.Span.Start), T($"{{{value}}}", PromptTokenKind.Reference, source.Span.Start)]);
        return new CommandSyntax(tokens, grammar);
    }

    private static PromptToken T(string text, PromptTokenKind kind, int start) => new(text, kind, Math.Max(0, start), 0);

    private static CommandSyntax? Error(ICollection<SurfaceDiagnostic> diagnostics, string code, string message, SurfaceCommandSyntax syntax)
    {
        diagnostics.Add(new SurfaceDiagnostic(code, message, syntax.Span));
        return null;
    }
}
