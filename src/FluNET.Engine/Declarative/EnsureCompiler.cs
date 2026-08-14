using FluNET.Automation;
using FluNET.Compilation;
using FluNET.Prompt;
using FluNET.Prompt.Surface;
using System.Security.Cryptography;
using System.Text;

namespace FluNET.Declarative;

/// <summary>Compiles ENSURE goals into normal compact AST and the canonical execution pipeline.</summary>
public sealed class EnsureCompiler(SurfaceCompiler surfaceCompiler)
{
    public DesiredStateCompilationResult Compile(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        List<DesiredStateDiagnostic> diagnostics = [];
        Line[] lines = ReadLines(source).Where(line => !string.IsNullOrWhiteSpace(line.Text) && !line.Text.TrimStart().StartsWith('#')).ToArray();
        List<DesiredStatePlan> plans = [];
        int cursor = 0;
        while (cursor < lines.Length)
        {
            Line header = lines[cursor++];
            string text = header.Text.Trim();
            if (!text.StartsWith("ENSURE ", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new("FLN320", "Expected ENSURE goal.", header.Span));
                continue;
            }
            int contains = text.IndexOf(" CONTAINS ", StringComparison.OrdinalIgnoreCase);
            if (contains <= 7 || contains + 10 >= text.Length)
            {
                diagnostics.Add(new("FLN321", "ENSURE requires `ENSURE target CONTAINS resource`.", header.Span));
                continue;
            }
            string target = text[7..contains].Trim();
            string resource = text[(contains + 10)..].Trim();
            TimeSpan? refresh = null;
            int? keep = null;
            bool notify = false;
            while (cursor < lines.Length && !lines[cursor].Text.TrimStart().StartsWith("ENSURE ", StringComparison.OrdinalIgnoreCase))
            {
                Line option = lines[cursor++];
                string optionText = option.Text.Trim();
                if (optionText.StartsWith("REFRESH EVERY ", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryDuration(optionText[14..].Trim(), out TimeSpan interval))
                        diagnostics.Add(new("FLN322", $"Invalid REFRESH interval '{optionText[14..].Trim()}'.", option.Span));
                    else refresh = interval;
                }
                else if (optionText.StartsWith("KEEP ", StringComparison.OrdinalIgnoreCase) && optionText.EndsWith(" VERSIONS", StringComparison.OrdinalIgnoreCase))
                {
                    string countText = optionText[5..^9].Trim();
                    if (!int.TryParse(countText, out int count) || count <= 0 || count > 10000)
                        diagnostics.Add(new("FLN323", "KEEP requires a version count between 1 and 10000.", option.Span));
                    else keep = count;
                }
                else if (optionText.Equals("NOTIFY ON FAILURE", StringComparison.OrdinalIgnoreCase)) notify = true;
                else diagnostics.Add(new("FLN324", $"Unknown ENSURE option '{optionText}'.", option.Span));
            }

            EnsureGoal goal = new(target, resource, refresh, keep, notify, header.Span);
            SourceDocument document = new(source, SourceSyntaxKind.Compact);
            string output = $"__ensure_{plans.Count:D4}";
            SurfaceCommandSyntax get = new("GET", [new SurfaceValueSyntax(resource, header.Span)], output, header.Span);
            SurfaceCommandSyntax save = new("SAVE", [new SurfaceValueSyntax($"{output} TO {target}", header.Span)], null, header.Span);
            SurfaceProgramSyntax program = new([get, save], header.Span);
            SurfaceCompilationResult compilation = surfaceCompiler.Compile(document, program);
            AutomationDefinition? automation = refresh is TimeSpan intervalValue && compilation.IsValid
                ? new AutomationDefinition(
                    StableId(target, resource),
                    new IntervalTriggerDefinition(intervalValue),
                    new WorkflowTemplate(compilation),
                    header.Span)
                : null;
            plans.Add(new DesiredStatePlan(goal, compilation, automation));
        }
        return new(plans, diagnostics);
    }

    private static string StableId(string target, string source)
    {
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"ensure|{target}|{source}"))).ToLowerInvariant()[..12];
        return $"ensure-{hash}";
    }

    private static bool TryDuration(string source, out TimeSpan interval)
    {
        string text = source.Trim().ToLowerInvariant();
        (string Number, double Seconds) part = text switch
        {
            _ when text.EndsWith('s') => (text[..^1], 1),
            _ when text.EndsWith('m') => (text[..^1], 60),
            _ when text.EndsWith('h') => (text[..^1], 3600),
            _ when text.EndsWith('d') => (text[..^1], 86400),
            _ => (string.Empty, 0)
        };
        if (part.Number.Length == 0 || !double.TryParse(part.Number, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double number) || number <= 0)
        { interval = default; return false; }
        interval = TimeSpan.FromSeconds(number * part.Seconds);
        return interval > TimeSpan.Zero && interval <= TimeSpan.FromDays(365);
    }

    private static IEnumerable<Line> ReadLines(string source)
    {
        int start = 0;
        for (int index = 0; index <= source.Length; index++)
        {
            if (index < source.Length && source[index] != '\n') continue;
            int length = index - start;
            if (length > 0 && source[start + length - 1] == '\r') length--;
            string text = source.Substring(start, length);
            yield return new Line(text, new SourceSpan(start, Math.Max(1, length)));
            start = index + 1;
        }
    }
    private sealed record Line(string Text, SourceSpan Span);
}
