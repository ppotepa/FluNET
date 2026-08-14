using FluNET.Variables;
using System.Text.Json;

namespace FluNET.Execution.Commands;

/// <summary>Compiled row projection shared by SELECT and MAP surface stages.</summary>
public sealed class JsonProjection
{
    private readonly Field[] _fields;

    private JsonProjection(IEnumerable<Field> fields)
    {
        _fields = fields?.ToArray() ?? throw new ArgumentNullException(nameof(fields));
        if (_fields.Length == 0)
        {
            throw new FormatException("A JSON projection must contain at least one field.");
        }
        if (_fields.Select(field => field.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != _fields.Length)
        {
            throw new FormatException("A JSON projection cannot declare the same output field more than once.");
        }
    }

    public static JsonProjection Select(string source) =>
        new(SplitFields(source).Select(ParseField));

    public static JsonProjection Map(string source)
    {
        string normalized = source.Trim();
        if (normalized.Length >= 2 && normalized[0] == '{' && normalized[^1] == '}')
        {
            normalized = normalized[1..^1];
        }
        return new JsonProjection(SplitFields(normalized).Select(ParseField));
    }

    public JsonElement Evaluate(JsonElement row, IVariableResolver variables)
    {
        Dictionary<string, object?> value = new(StringComparer.OrdinalIgnoreCase);
        foreach (Field field in _fields)
        {
            value[field.Name] = field.Expression.Evaluate(row, variables);
        }
        return JsonSerializer.SerializeToElement(value).Clone();
    }

    private static Field ParseField(string source)
    {
        string text = source.Trim();
        int colon = TopLevelColon(text);
        string name;
        string expression;
        if (colon >= 0)
        {
            name = text[..colon].Trim();
            expression = text[(colon + 1)..].Trim();
        }
        else
        {
            expression = text;
            name = SuggestedName(expression);
        }
        if (!IsIdentifier(name) || expression.Length == 0)
        {
            throw new FormatException($"Invalid projection field '{source}'. Use `name` or `name: expression`.");
        }
        return new Field(name, JsonDataExpression.Parse(expression));
    }

    private static string SuggestedName(string expression)
    {
        string trimmed = expression.Trim();
        int dot = trimmed.LastIndexOf('.');
        string candidate = dot >= 0 ? trimmed[(dot + 1)..] : trimmed;
        int bracket = candidate.IndexOf('[');
        if (bracket >= 0) candidate = candidate[..bracket];
        return candidate.Trim();
    }

    private static IReadOnlyList<string> SplitFields(string source)
    {
        List<string> fields = [];
        int start = 0;
        int depth = 0;
        char? quote = null;
        bool escaped = false;
        for (int index = 0; index <= source.Length; index++)
        {
            bool atEnd = index == source.Length;
            char current = atEnd ? '\0' : source[index];
            if (!atEnd)
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
            if (!atEnd && (current != ',' || depth != 0)) continue;
            string field = source[start..index].Trim();
            if (field.Length == 0) throw new FormatException("Projection fields cannot be empty.");
            fields.Add(field);
            start = index + 1;
        }
        if (quote is not null || depth != 0) throw new FormatException("Unclosed delimiter in projection.");
        return fields;
    }

    private static int TopLevelColon(string source)
    {
        int depth = 0;
        char? quote = null;
        bool escaped = false;
        for (int index = 0; index < source.Length; index++)
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
            if (current == ':' && depth == 0) return index;
        }
        return -1;
    }

    private static bool IsIdentifier(string value) =>
        value.Length > 0 &&
        (char.IsLetter(value[0]) || value[0] == '_') &&
        value.Skip(1).All(character => char.IsLetterOrDigit(character) || character == '_');

    private sealed record Field(string Name, JsonDataExpression Expression);
}
