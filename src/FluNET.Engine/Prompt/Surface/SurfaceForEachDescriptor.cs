using System.Text;
using System.Text.Json;

namespace FluNET.Prompt.Surface;

/// <summary>Compile-time descriptor for a compact FOR EACH block.</summary>
public sealed record SurfaceForEachDescriptor(
    string ItemName,
    int MaxConcurrency,
    IReadOnlyList<SurfaceIterationActionDescriptor> Actions)
{
    public string Encode() => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this)));

    public static SurfaceForEachDescriptor Decode(string value)
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(value);
            return JsonSerializer.Deserialize<SurfaceForEachDescriptor>(bytes)
                ?? throw new FormatException("FOR EACH descriptor is empty.");
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new FormatException("Invalid FOR EACH descriptor.", exception);
        }
    }

    internal static bool TryCreate(
        SurfaceCommandSyntax header,
        IReadOnlyList<SurfaceStatementSyntax> body,
        ICollection<SurfaceDiagnostic> diagnostics,
        out SurfaceForEachDescriptor? descriptor)
    {
        descriptor = null;
        if (header.Values.Count != 1)
        {
            diagnostics.Add(new SurfaceDiagnostic("FLN270", "FOR EACH requires `FOR EACH item [PARALLEL n]`.", header.Span));
            return false;
        }

        string[] tokens = header.Values[0].UnquotedText
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length < 2 || !tokens[0].Equals("EACH", StringComparison.OrdinalIgnoreCase) || !IsIdentifier(tokens[1]))
        {
            diagnostics.Add(new SurfaceDiagnostic("FLN270", "FOR EACH requires `FOR EACH item [PARALLEL n]`.", header.Span));
            return false;
        }

        int concurrency = 4;
        if (tokens.Length > 2)
        {
            if (tokens.Length != 4 || !tokens[2].Equals("PARALLEL", StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(tokens[3], out concurrency) || concurrency <= 0 || concurrency > 256)
            {
                diagnostics.Add(new SurfaceDiagnostic("FLN271", "PARALLEL must be an integer between 1 and 256.", header.Span));
                return false;
            }
        }

        List<SurfaceIterationActionDescriptor> actions = [];
        foreach (SurfaceStatementSyntax statement in body)
        {
            if (statement is not SurfaceCommandSyntax command || command.NormalizedName != "SAY")
            {
                diagnostics.Add(new SurfaceDiagnostic(
                    "FLN272",
                    "FOR EACH currently compiles SAY actions; resource enrichment is provided by the later provider/action layer.",
                    statement.Span));
                return false;
            }
            string source = string.Join(" ", command.Values.Select(value => value.Text));
            if (source.Length == 0)
            {
                diagnostics.Add(new SurfaceDiagnostic("FLN273", "SAY inside FOR EACH requires text.", command.Span));
                return false;
            }
            actions.Add(new SurfaceIterationActionDescriptor("SAY", source));
        }
        if (actions.Count == 0)
        {
            diagnostics.Add(new SurfaceDiagnostic("FLN274", "FOR EACH requires at least one action.", header.Span));
            return false;
        }
        descriptor = new SurfaceForEachDescriptor(tokens[1], concurrency, actions);
        return true;
    }

    private static bool IsIdentifier(string value) =>
        value.Length > 0 &&
        (char.IsLetter(value[0]) || value[0] == '_') &&
        value.Skip(1).All(character => char.IsLetterOrDigit(character) || character == '_');
}

public sealed record SurfaceIterationActionDescriptor(string Kind, string Source);
