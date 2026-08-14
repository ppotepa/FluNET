using FluNET.Compilation;
using FluNET.Compilation.Inference;
using FluNET.Language;
using FluNET.Language.Resources;
using FluNET.Prompt;
using FluNET.Prompt.Surface;

namespace FluNET.Declarative.Reconciliation;

public enum SyncDirection
{
    SourceToTarget
}

public sealed record SyncGoal(
    string TargetResource,
    string SourceResource,
    string KeyField,
    SyncDirection Direction,
    SourceSpan Span);

public sealed record SyncDefinition(
    SyncGoal Goal,
    ResourceDescriptor TargetDescriptor,
    ResourceDescriptor SourceDescriptor,
    SurfaceCompilationResult ReadCompilation,
    string TargetVariable,
    string SourceVariable)
{
    public bool IsValid => ReadCompilation.IsValid;
}

public sealed record SyncDiagnostic(string Code, string Message, SourceSpan Span);

public sealed record SyncCompilationResult(
    IReadOnlyList<SyncDefinition> Definitions,
    IReadOnlyList<SyncDiagnostic> Diagnostics)
{
    public bool IsValid => Diagnostics.Count == 0 && Definitions.Count > 0 && Definitions.All(item => item.IsValid);
}

/// <summary>
/// Compiles `SYNC target WITH source BY key`. The right side is the desired/source-of-truth;
/// the left side is the observed mutation target. The generated read graph is analysis-only
/// metadata for the definition; runtime reconciliation uses the resource-observation boundary.
/// </summary>
public sealed class SyncCompiler(
    SurfaceCompiler surfaceCompiler,
    LanguageSnapshot language)
{
    private readonly InferenceEngine _inference = new();

    public SyncCompilationResult Compile(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        List<SyncDefinition> definitions = [];
        List<SyncDiagnostic> diagnostics = [];
        int index = 0;

        foreach (Statement statement in ReadStatements(source))
        {
            string text = statement.Text.Trim();
            if (text.Length == 0 || text.StartsWith('#')) continue;
            if (!text.StartsWith("SYNC ", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new("FLN340", "Expected `SYNC target WITH source BY key`.", statement.Span));
                continue;
            }

            string body = text[5..].Trim();
            int with = FindTopLevel(body, " WITH ");
            int by = with < 0 ? -1 : FindTopLevel(body, " BY ", with + 6);
            if (with <= 0 || by <= with + 6 || by + 4 >= body.Length)
            {
                diagnostics.Add(new("FLN340", "SYNC requires `SYNC target WITH source BY key`.", statement.Span));
                continue;
            }

            string target = body[..with].Trim();
            string desired = body[(with + 6)..by].Trim();
            string key = body[(by + 4)..].Trim();
            if (target.Length == 0 || desired.Length == 0)
            {
                diagnostics.Add(new("FLN340", "SYNC target and source cannot be empty.", statement.Span));
                continue;
            }
            if (!IsKeyField(key))
            {
                diagnostics.Add(new("FLN341", $"Invalid SYNC key field '{key}'. Use one top-level field name.", statement.Span));
                continue;
            }

            ResourceDescriptor targetDescriptor;
            ResourceDescriptor sourceDescriptor;
            try
            {
                targetDescriptor = _inference.InferResource(new SurfaceValueSyntax(target, statement.Span), language);
                sourceDescriptor = _inference.InferResource(new SurfaceValueSyntax(desired, statement.Span), language);
            }
            catch (FormatException exception)
            {
                diagnostics.Add(new("FLN342", exception.Message, statement.Span));
                continue;
            }

            string targetVariable = $"__sync_target_{index:D4}";
            string sourceVariable = $"__sync_source_{index:D4}";
            string readSource = $"GET {target} AS {targetVariable}{Environment.NewLine}GET {desired} AS {sourceVariable}";
            SurfaceCompilationResult read = surfaceCompiler.Compile(
                new SourceDocument(readSource, SourceSyntaxKind.Compact));
            if (!read.IsValid)
            {
                diagnostics.Add(new(
                    "FLN343",
                    $"SYNC read graph does not compile for '{target}' and '{desired}'.",
                    statement.Span));
            }

            SyncGoal goal = new(
                target,
                desired,
                key,
                SyncDirection.SourceToTarget,
                statement.Span);
            definitions.Add(new(
                goal,
                targetDescriptor,
                sourceDescriptor,
                read,
                targetVariable,
                sourceVariable));
            index++;
        }

        if (definitions.Count == 0 && diagnostics.Count == 0)
            diagnostics.Add(new("FLN340", "SYNC source contains no definitions.", default));
        return new(definitions, diagnostics);
    }

    private static bool IsKeyField(string value) =>
        value.Length > 0 &&
        (char.IsLetter(value[0]) || value[0] == '_') &&
        value.Skip(1).All(character => char.IsLetterOrDigit(character) || character is '_' or '-');

    private static int FindTopLevel(string source, string keyword, int start = 0)
    {
        char? quote = null;
        bool escaped = false;
        int depth = 0;
        for (int index = Math.Max(0, start); index <= source.Length - keyword.Length; index++)
        {
            char current = source[index];
            if (escaped) { escaped = false; continue; }
            if (quote is not null)
            {
                if (current == '\\') escaped = true;
                else if (current == quote) quote = null;
                continue;
            }
            if (current is '"' or '\'') { quote = current; continue; }
            if (current is '(' or '[' or '{') { depth++; continue; }
            if (current is ')' or ']' or '}') { depth = Math.Max(0, depth - 1); continue; }
            if (depth == 0 && source.AsSpan(index).StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
                return index;
        }
        return -1;
    }

    private static IEnumerable<Statement> ReadStatements(string source)
    {
        int lineStart = 0;
        for (int index = 0; index <= source.Length; index++)
        {
            if (index < source.Length && source[index] != '\n') continue;
            int length = index - lineStart;
            if (length > 0 && source[lineStart + length - 1] == '\r') length--;
            string line = source.Substring(lineStart, length);
            foreach (Statement statement in SplitLine(line, lineStart)) yield return statement;
            lineStart = index + 1;
        }
    }

    private static IEnumerable<Statement> SplitLine(string line, int absoluteStart)
    {
        int segment = 0;
        char? quote = null;
        bool escaped = false;
        int depth = 0;
        for (int index = 0; index <= line.Length; index++)
        {
            bool end = index == line.Length;
            char current = end ? '\0' : line[index];
            if (!end)
            {
                if (escaped) { escaped = false; continue; }
                if (quote is not null)
                {
                    if (current == '\\') escaped = true;
                    else if (current == quote) quote = null;
                    continue;
                }
                if (current is '"' or '\'') { quote = current; continue; }
                if (current is '(' or '[' or '{') { depth++; continue; }
                if (current is ')' or ']' or '}') { depth = Math.Max(0, depth - 1); continue; }
            }
            if (!end && (current != ';' || depth != 0)) continue;
            string text = line[segment..index].Trim();
            if (text.Length > 0)
            {
                int local = line.IndexOf(text, segment, StringComparison.Ordinal);
                yield return new(text, new SourceSpan(absoluteStart + local, text.Length));
            }
            segment = index + 1;
        }
    }

    private sealed record Statement(string Text, SourceSpan Span);
}
