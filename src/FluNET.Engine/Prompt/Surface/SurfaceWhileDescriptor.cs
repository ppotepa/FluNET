using System.Text;
using System.Text.Json;

namespace FluNET.Prompt.Surface;

/// <summary>Bounded runtime loop descriptor using the portable nested-action contract.</summary>
public sealed record SurfaceWhileDescriptor(
    string Condition,
    int MaxIterations,
    IReadOnlyList<SurfaceIterationActionDescriptor> Actions)
{
    public string Encode() => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this)));

    public static SurfaceWhileDescriptor Decode(string value)
    {
        try
        {
            return JsonSerializer.Deserialize<SurfaceWhileDescriptor>(Convert.FromBase64String(value))
                ?? throw new FormatException("WHILE descriptor is empty.");
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new FormatException("Invalid WHILE descriptor.", exception);
        }
    }

    internal static bool TryCreate(
        SurfaceCommandSyntax header,
        IReadOnlyList<SurfaceStatementSyntax> body,
        ICollection<SurfaceDiagnostic> diagnostics,
        out SurfaceWhileDescriptor? descriptor)
    {
        descriptor = null;
        if (header.Values.Count != 1)
        {
            diagnostics.Add(new("FLN361", "WHILE requires a condition and optional MAX count, for example `WHILE ready == false MAX 100`.", header.Span));
            return false;
        }

        string phrase = header.Values[0].UnquotedText.Trim().TrimEnd(':').Trim();
        int max = 1000;
        System.Text.RegularExpressions.Match limit = System.Text.RegularExpressions.Regex.Match(
            phrase,
            @"^(?<condition>.+?)\s+MAX(?:\s+ITERATIONS?)?\s+(?<count>[0-9]+)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        string condition = phrase;
        if (limit.Success)
        {
            condition = limit.Groups["condition"].Value.Trim();
            if (!int.TryParse(limit.Groups["count"].Value, out max) || max < 1 || max > 100_000)
            {
                diagnostics.Add(new("FLN362", "WHILE MAX must be between 1 and 100000.", header.Span));
                return false;
            }
        }
        if (condition.Length == 0)
        {
            diagnostics.Add(new("FLN361", "WHILE requires a non-empty condition.", header.Span));
            return false;
        }

        SurfaceCommandSyntax fakeFor = new("FOR", [new SurfaceValueSyntax("EACH loop", header.Span)], null, header.Span);
        if (!SurfaceForEachDescriptor.TryCreate(fakeFor, body, diagnostics, out SurfaceForEachDescriptor? actions, allowLoopControl: true))
            return false;
        descriptor = new SurfaceWhileDescriptor(condition, max, actions!.Actions);
        return true;
    }
}
