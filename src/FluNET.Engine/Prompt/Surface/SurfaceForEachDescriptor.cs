using System.Text;
using System.Text.Json;

namespace FluNET.Prompt.Surface;

/// <summary>Compile-time descriptor for a compact FOR EACH block.</summary>
public sealed record SurfaceForEachDescriptor(
    string ItemName,
    int MaxConcurrency,
    IReadOnlyList<SurfaceIterationActionDescriptor> Actions,
    string? SourceName = null)
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
        out SurfaceForEachDescriptor? descriptor,
        bool allowLoopControl = false)
    {
        descriptor = null;
        if (header.Values.Count != 1)
        {
            diagnostics.Add(new SurfaceDiagnostic("FLN270", "FOR EACH requires `FOR EACH item [IN collection] [PARALLEL n]`.", header.Span));
            return false;
        }

        string[] tokens = header.Values[0].UnquotedText
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length < 2 || !tokens[0].Equals("EACH", StringComparison.OrdinalIgnoreCase) || !IsIdentifier(tokens[1]))
        {
            diagnostics.Add(new SurfaceDiagnostic("FLN270", "FOR EACH requires `FOR EACH item [IN collection] [PARALLEL n]`.", header.Span));
            return false;
        }

        string itemName = tokens[1];
        string? sourceName = null;
        int concurrency = 4;
        int cursor = 2;
        if (cursor < tokens.Length && tokens[cursor].Equals("IN", StringComparison.OrdinalIgnoreCase))
        {
            if (++cursor >= tokens.Length)
            {
                diagnostics.Add(new SurfaceDiagnostic("FLN270", "IN must be followed by a collection variable.", header.Span));
                return false;
            }
            sourceName = NormalizeVariable(tokens[cursor++]);
            if (!IsIdentifier(sourceName))
            {
                diagnostics.Add(new SurfaceDiagnostic("FLN270", "FOR EACH source must be a collection variable name.", header.Span));
                return false;
            }
        }
        if (cursor < tokens.Length)
        {
            if (cursor + 2 != tokens.Length || !tokens[cursor].Equals("PARALLEL", StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(tokens[cursor + 1], out concurrency) || concurrency <= 0 || concurrency > 256)
            {
                diagnostics.Add(new SurfaceDiagnostic("FLN271", "PARALLEL must be an integer between 1 and 256.", header.Span));
                return false;
            }
            cursor += 2;
        }
        if (cursor != tokens.Length)
        {
            diagnostics.Add(new SurfaceDiagnostic("FLN270", "Unexpected FOR EACH header tokens.", header.Span));
            return false;
        }

        List<SurfaceIterationActionDescriptor> actions = [];
        foreach (SurfaceStatementSyntax statement in body)
        {
            if (statement is not SurfaceCommandSyntax command)
            {
            diagnostics.Add(new SurfaceDiagnostic("FLN272", "FOR EACH body supports SAY/NOTIFY/PUBLISH/GET/LOAD/SAVE/POST and portable filesystem actions.", statement.Span));
                return false;
            }
            if (!TryAction(command, diagnostics, out SurfaceIterationActionDescriptor? action, allowLoopControl)) return false;
            actions.Add(action!);
        }
        if (actions.Count == 0)
        {
            diagnostics.Add(new SurfaceDiagnostic("FLN274", "FOR EACH requires at least one action.", header.Span));
            return false;
        }
        descriptor = new SurfaceForEachDescriptor(itemName, concurrency, actions, sourceName);
        return true;
    }

    private static bool TryAction(
        SurfaceCommandSyntax command,
        ICollection<SurfaceDiagnostic> diagnostics,
        out SurfaceIterationActionDescriptor? action,
        bool allowLoopControl)
    {
        action = null;
        switch (command.NormalizedName)
        {
            case "SAY":
            {
                if (command.Alias is not null || command.Values.Count == 0)
                {
                    diagnostics.Add(new SurfaceDiagnostic("FLN273", "SAY inside FOR EACH requires text and cannot use AS.", command.Span));
                    return false;
                }
                action = new("SAY", string.Join(" ", command.Values.Select(value => value.Text)));
                return true;
            }
            case "GET":
            case "LOAD":
            {
                if (command.Values.Count != 1 || string.IsNullOrWhiteSpace(command.Alias))
                {
                    diagnostics.Add(new SurfaceDiagnostic("FLN272", $"{command.NormalizedName} inside FOR EACH requires one resource and `AS alias`.", command.Span));
                    return false;
                }
                action = new(command.NormalizedName, command.Values[0].UnquotedText, command.Alias);
                return true;
            }
            case "SAVE":
            case "POST":
            {
                if (command.Values.Count != 1 || command.Alias is not null || !TryValueToTarget(command.Values[0].UnquotedText, out string? value, out string? target))
                {
                    diagnostics.Add(new SurfaceDiagnostic("FLN272", $"{command.NormalizedName} inside FOR EACH requires `value TO target`.", command.Span));
                    return false;
                }
                action = new(command.NormalizedName, value!, null, target);
                return true;
            }
            case "PUBLISH":
            {
                if (command.Values.Count != 1 || command.Alias is not null ||
                    !TryValueToTarget(command.Values[0].UnquotedText, out string? payload, out string? topic))
                {
                    diagnostics.Add(new SurfaceDiagnostic("FLN272", "PUBLISH inside FOR EACH requires `payload TO topic` and no alias.", command.Span));
                    return false;
                }
                action = new("PUBLISH", payload!, null, topic);
                return true;
            }
            case "NOTIFY":
            {
                if (command.Values.Count == 0 || command.Alias is not null)
                {
                    diagnostics.Add(new SurfaceDiagnostic("FLN272", "NOTIFY inside FOR EACH requires a message and no alias.", command.Span));
                    return false;
                }
                action = new("NOTIFY", string.Join(" ", command.Values.Select(value => value.Text)));
                return true;
            }
            case "INCREMENT":
            {
                if (command.Values.Count != 1 || command.Alias is not null)
                {
                    diagnostics.Add(new SurfaceDiagnostic("FLN272", "INCREMENT inside a loop requires one variable and no alias.", command.Span));
                    return false;
                }
                action = new("INCREMENT", command.Values[0].UnquotedText.Trim());
                return true;
            }
            case "SET":
            {
                if (command.Values.Count != 1 || command.Alias is not null)
                {
                    diagnostics.Add(new SurfaceDiagnostic("FLN272", "SET inside a loop requires `SET [variable] TO value`.", command.Span));
                    return false;
                }
                string phrase = command.Values[0].UnquotedText.Trim();
                int marker = phrase.IndexOf(" TO ", StringComparison.OrdinalIgnoreCase);
                if (marker <= 0 || marker + 4 >= phrase.Length)
                {
                    diagnostics.Add(new SurfaceDiagnostic("FLN272", "SET inside a loop requires `SET [variable] TO value`.", command.Span));
                    return false;
                }
                string variable = phrase[..marker].Trim();
                string value = phrase[(marker + 4)..].Trim();
                action = new("SET", value, variable);
                return true;
            }
            case "BREAK" or "CONTINUE":
            {
                if (!allowLoopControl || command.Alias is not null || command.Values.Count > 1)
                {
                    diagnostics.Add(new SurfaceDiagnostic("FLN275", "BREAK/CONTINUE are available in WHILE bodies as `BREAK WHEN condition` or `CONTINUE WHEN condition`.", command.Span));
                    return false;
                }

                string condition = command.Values.Count == 0
                    ? "true"
                    : command.Values[0].UnquotedText.Trim();
                if (condition.StartsWith("WHEN ", StringComparison.OrdinalIgnoreCase))
                    condition = condition[5..].Trim();
                if (condition.Length == 0)
                {
                    diagnostics.Add(new SurfaceDiagnostic("FLN275", "BREAK/CONTINUE WHEN requires a condition.", command.Span));
                    return false;
                }
                action = new(command.NormalizedName, condition);
                return true;
            }
            case "MKDIR":
            case "TRASH":
            {
                if (command.Values.Count != 1 || command.Alias is not null)
                {
                    diagnostics.Add(new SurfaceDiagnostic("FLN272", $"{command.NormalizedName} inside FOR EACH requires one path and no alias.", command.Span));
                    return false;
                }
                action = new(command.NormalizedName, command.Values[0].UnquotedText);
                return true;
            }
            case "COPY":
            case "MOVE":
            case "PACK":
            case "UNPACK":
            {
                if (command.Values.Count != 1 || command.Alias is not null ||
                    !TryValueToTarget(command.Values[0].UnquotedText, out string? source, out string? target))
                {
                    diagnostics.Add(new SurfaceDiagnostic("FLN272", $"{command.NormalizedName} inside FOR EACH requires `source TO target`.", command.Span));
                    return false;
                }
                action = new(command.NormalizedName, source!, null, target);
                return true;
            }
            default:
                diagnostics.Add(new SurfaceDiagnostic("FLN272", $"FOR EACH action '{command.Name}' is not supported. Use SAY, NOTIFY, PUBLISH, GET, LOAD, SAVE, POST, MKDIR, COPY, MOVE, PACK, UNPACK or TRASH.", command.Span));
                return false;
        }
    }

    private static bool TryValueToTarget(string source, out string? value, out string? target)
    {
        int marker = source.IndexOf(" TO ", StringComparison.OrdinalIgnoreCase);
        if (marker <= 0 || marker + 4 >= source.Length) { value = target = null; return false; }
        value = source[..marker].Trim(); target = source[(marker + 4)..].Trim();
        return value.Length > 0 && target.Length > 0;
    }

    private static string NormalizeVariable(string value) => value.Trim().TrimStart('[').TrimEnd(']');
    private static bool IsIdentifier(string value) => value.Length > 0 &&
        (char.IsLetter(value[0]) || value[0] == '_') &&
        value.Skip(1).All(character => char.IsLetterOrDigit(character) || character == '_');
}

public sealed record SurfaceIterationActionDescriptor(
    string Kind,
    string Source,
    string? Alias = null,
    string? Target = null);
