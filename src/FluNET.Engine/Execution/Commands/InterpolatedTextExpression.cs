using FluNET.Language;
using FluNET.Language.Values;
using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace FluNET.Execution.Commands;

/// <summary>Runtime path such as post.title or posts[0].title rooted in a FluNET variable.</summary>
public sealed class DynamicPathExpression : IExpression<object?>
{
    private readonly IReadOnlyList<PathSegment> _segments;

    private DynamicPathExpression(string root, IReadOnlyList<PathSegment> segments)
    {
        Root = root;
        _segments = segments;
    }

    public string Root { get; }

    public static bool TryParse(string source, out DynamicPathExpression? expression)
    {
        expression = null;
        if (string.IsNullOrWhiteSpace(source)) return false;
        string text = source.Trim();
        int position = 0;
        if (!ReadIdentifier(text, ref position, out string root)) return false;
        List<PathSegment> segments = [];
        while (position < text.Length)
        {
            if (text[position] == '.')
            {
                position++;
                if (!ReadIdentifier(text, ref position, out string property)) return false;
                segments.Add(new PropertySegment(property));
                continue;
            }
            if (text[position] == '[')
            {
                int close = text.IndexOf(']', position + 1);
                if (close < 0) return false;
                string value = text[(position + 1)..close].Trim().Trim('"', '\'');
                if (value.Length == 0) return false;
                segments.Add(int.TryParse(value, out int index)
                    ? new IndexSegment(index)
                    : new PropertySegment(value));
                position = close + 1;
                continue;
            }
            return false;
        }
        expression = new DynamicPathExpression(root, segments);
        return true;
    }

    public object? Evaluate(IExpressionEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        object? current = context.Variables.Resolve<object>($"[{Root}]")
            ?? throw new InvalidOperationException($"Variable [{Root}] not found in context.");
        foreach (PathSegment segment in _segments)
        {
            current = segment.Read(current);
        }
        return current;
    }

    private static bool ReadIdentifier(string text, ref int position, out string value)
    {
        int start = position;
        if (position >= text.Length || !(char.IsLetter(text[position]) || text[position] == '_'))
        {
            value = string.Empty;
            return false;
        }
        position++;
        while (position < text.Length && (char.IsLetterOrDigit(text[position]) || text[position] == '_')) position++;
        value = text[start..position];
        return true;
    }

    private abstract record PathSegment
    {
        public abstract object? Read(object? source);
    }

    private sealed record PropertySegment(string Name) : PathSegment
    {
        public override object? Read(object? source)
        {
            if (source is null) return null;
            if (source is JsonElement json && json.ValueKind == JsonValueKind.Object)
            {
                if (json.TryGetProperty(Name, out JsonElement exact)) return JsonValue(exact);
                foreach (JsonProperty property in json.EnumerateObject())
                {
                    if (property.Name.Equals(Name, StringComparison.OrdinalIgnoreCase)) return JsonValue(property.Value);
                }
                return null;
            }
            if (source is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key?.ToString()?.Equals(Name, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        return entry.Value;
                    }
                }
                return null;
            }
            PropertyInfo? propertyInfo = source.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(candidate => candidate.Name.Equals(Name, StringComparison.OrdinalIgnoreCase));
            return propertyInfo?.GetValue(source);
        }
    }

    private sealed record IndexSegment(int Index) : PathSegment
    {
        public override object? Read(object? source)
        {
            if (Index < 0 || source is null) return null;
            if (source is JsonElement json && json.ValueKind == JsonValueKind.Array)
            {
                return Index < json.GetArrayLength() ? JsonValue(json[Index]) : null;
            }
            if (source is IList list) return Index < list.Count ? list[Index] : null;
            if (source is Array array) return Index < array.Length ? array.GetValue(Index) : null;
            return null;
        }
    }

    private static object? JsonValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number when value.TryGetDecimal(out decimal number) => number,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => value.Clone()
    };
}

/// <summary>Text template whose placeholders are normal dynamic path expressions.</summary>
public sealed class InterpolatedTextExpression : IExpression<string>
{
    private readonly IReadOnlyList<TemplatePart> _parts;
    private readonly LanguageSnapshot _language;
    private readonly IValueCodecRegistry _values;

    private InterpolatedTextExpression(
        IReadOnlyList<TemplatePart> parts,
        LanguageSnapshot language,
        IValueCodecRegistry values)
    {
        _parts = parts;
        _language = language;
        _values = values;
        VariableReferences = parts
            .OfType<PathPart>()
            .Select(part => part.Path.Root)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<string> VariableReferences { get; }

    public static bool TryCreate(
        string template,
        LanguageSnapshot language,
        IValueCodecRegistry values,
        out IExpression<string>? expression)
    {
        ArgumentNullException.ThrowIfNull(template);
        List<TemplatePart> parts = [];
        StringBuilder literal = new();
        bool found = false;
        for (int index = 0; index < template.Length; index++)
        {
            if (template[index] == '{' && index + 1 < template.Length && template[index + 1] == '{')
            {
                literal.Append('{'); index++; continue;
            }
            if (template[index] == '}' && index + 1 < template.Length && template[index + 1] == '}')
            {
                literal.Append('}'); index++; continue;
            }
            if (template[index] != '{')
            {
                literal.Append(template[index]);
                continue;
            }

            int close = template.IndexOf('}', index + 1);
            if (close < 0)
            {
                expression = null;
                return false;
            }
            string pathText = template[(index + 1)..close].Trim();
            if (!DynamicPathExpression.TryParse(pathText, out DynamicPathExpression? path))
            {
                literal.Append(template[index..(close + 1)]);
                index = close;
                continue;
            }
            if (literal.Length > 0)
            {
                parts.Add(new LiteralPart(literal.ToString()));
                literal.Clear();
            }
            parts.Add(new PathPart(path!));
            found = true;
            index = close;
        }
        if (literal.Length > 0) parts.Add(new LiteralPart(literal.ToString()));
        expression = found ? new InterpolatedTextExpression(parts, language, values) : null;
        return found;
    }

    public string Evaluate(IExpressionEvaluationContext context)
    {
        StringBuilder result = new();
        foreach (TemplatePart part in _parts)
        {
            if (part is LiteralPart literal)
            {
                result.Append(literal.Text);
                continue;
            }
            object? value = ((PathPart)part).Path.Evaluate(context);
            result.Append(Format(value));
        }
        return result.ToString();
    }

    private string Format(object? value)
    {
        if (value is null) return string.Empty;
        if (value is string text) return text;
        TypeSymbol source = _language.Types.Get(value.GetType());
        ConversionResolution conversion = _values.ResolveConversion(source, _language.Types.Text);
        if (conversion.IsAmbiguous || conversion.Path is null)
        {
            throw new InvalidCastException($"No unique implicit conversion exists from '{source}' to Text.");
        }
        object formatted = _values.Convert(value, conversion.Path);
        return formatted as string
            ?? throw new InvalidCastException($"Conversion from '{source}' to Text did not return a string.");
    }

    private abstract record TemplatePart;
    private sealed record LiteralPart(string Text) : TemplatePart;
    private sealed record PathPart(DynamicPathExpression Path) : TemplatePart;
}
