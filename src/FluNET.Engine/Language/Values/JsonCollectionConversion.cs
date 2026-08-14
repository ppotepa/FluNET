using System.Text.Json;

namespace FluNET.Language.Values;

public sealed class JsonToJsonListConversion : IValueConversion<JsonElement, JsonElement[]>
{
    public JsonElement[] Convert(JsonElement value, ValueConversionContext context)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("Json value must be an array to be used as List<Json>.");
        }
        return value.EnumerateArray().Select(item => item.Clone()).ToArray();
    }
}
