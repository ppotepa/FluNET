using System.Text.RegularExpressions;

namespace FluNET.Prompt.Surface;

/// <summary>
/// Small, deterministic surface rewrites for the human-readable syntax.
/// These rewrites happen before semantic binding and never change runtime
/// frames or capability contracts.
/// </summary>
internal static partial class NaturalSurfaceSyntax
{
    public static string RewriteCommand(string source)
    {
        string text = source.Trim();
        if (text.Equals("AND THEN", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("AND THEN,", StringComparison.OrdinalIgnoreCase))
            return "THEN";

        text = Regex.Replace(text, @"^(THEN|SEQUENCE|PARALLEL|ELSE|OTHERWISE)\s*,?$", "$1", RegexOptions.IgnoreCase);

        Match set = SetValuePattern().Match(text);
        if (set.Success)
        {
            string name = (set.Groups["bracketed"].Success ? set.Groups["bracketed"].Value : set.Groups["plain"].Value).Trim();
            string value = set.Groups["value"].Value.Trim();
            if (IsIdentifier(name) && value.Length > 0)
                return $"LET {name} = {value}";
        }

        Match getFrom = GetFromPattern().Match(text);
        if (getFrom.Success && !text.Contains(" AS ", StringComparison.OrdinalIgnoreCase))
        {
            string output = getFrom.Groups[1].Value.Trim();
            string resource = getFrom.Groups[2].Value.Trim();
            if (IsIdentifier(output) && resource.Length > 0)
                return $"GET {resource} AS {output}";
        }

        Match count = CountSourcePattern().Match(text);
        if (count.Success)
            return $"COUNT [{count.Groups[1].Value}] AS {count.Groups[2].Value}";

        return text;
    }

    private static bool IsIdentifier(string value) =>
        value.Length > 0 &&
        (char.IsLetter(value[0]) || value[0] == '_') &&
        value.Skip(1).All(character => char.IsLetterOrDigit(character) || character == '_');

    [GeneratedRegex(@"^GET\s+([A-Za-z_][A-Za-z0-9_]*)\s+FROM\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GetFromPattern();

    [GeneratedRegex(@"^COUNT\s+([A-Za-z_][A-Za-z0-9_]*)\s+AS\s+([A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CountSourcePattern();

    [GeneratedRegex(@"^SET\s+(?:\[(?<bracketed>[A-Za-z_][A-Za-z0-9_]*)\]|(?<plain>[A-Za-z_][A-Za-z0-9_]*))\s+TO\s+(?<value>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SetValuePattern();
}
