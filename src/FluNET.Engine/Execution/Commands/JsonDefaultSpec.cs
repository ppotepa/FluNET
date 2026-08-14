using FluNET.Variables;
using System.Text.Json;

namespace FluNET.Execution.Commands;

/// <summary>Top-level JSON default assignment used by compact DEFAULT stages.</summary>
public sealed class JsonDefaultSpec
{
    private readonly JsonDataExpression _fallback;

    private JsonDefaultSpec(string field, JsonDataExpression fallback)
    {
        Field = string.IsNullOrWhiteSpace(field)
            ? throw new FormatException("DEFAULT field cannot be empty.")
            : field.Trim();
        _fallback = fallback;
    }

    public string Field { get; }

    public static JsonDefaultSpec Parse(string descriptor)
    {
        int separator = descriptor.IndexOf('|');
        if (separator <= 0 || separator == descriptor.Length - 1)
        {
            throw new FormatException("DEFAULT descriptor must contain `field|fallback`.");
        }
        return new JsonDefaultSpec(
            descriptor[..separator].Trim(),
            JsonDataExpression.Parse(descriptor[(separator + 1)..].Trim()));
    }

    public JsonElement Apply(JsonElement row, IVariableResolver variables)
    {
        if (row.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("DEFAULT requires JSON object rows.");
        }

        Dictionary<string, object?> value = new(StringComparer.OrdinalIgnoreCase);
        bool hasValue = false;
        foreach (JsonProperty property in row.EnumerateObject())
        {
            object? current = ConvertValue(property.Value);
            value[property.Name] = current;
            if (property.Name.Equals(Field, StringComparison.OrdinalIgnoreCase) && current is not null)
            {
                hasValue = true;
            }
        }
        if (!hasValue)
        {
            value[Field] = _fallback.Evaluate(row, variables);
        }
        return JsonSerializer.SerializeToElement(value).Clone();
    }

    private static object? ConvertValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number when value.TryGetDecimal(out decimal number) => number,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => value.Clone()
    };
}
