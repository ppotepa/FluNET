using System.Globalization;
using System.Text;
using System.Text.Json;

namespace FluNET.Language.Values;

internal static class SurfaceValue
{
    public static string Text(ValueLiteral literal)
    {
        if (literal.Value is string text)
        {
            return text;
        }

        string value = literal.Text;
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') ||
             (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1]
                .Replace("\\\"", "\"")
                .Replace("\\'", "'")
                .Replace("\\\\", "\\");
        }

        if (value.Length >= 2 && value[0] == '{' && value[^1] == '}')
        {
            return value[1..^1];
        }
        return literal.RawValue.ToString() ?? string.Empty;
    }
}

public sealed class TextValueCodec : IValueCodec<string>
{
    public string Parse(ValueLiteral literal, ValueParseContext context) =>
        SurfaceValue.Text(literal);

    public string Format(string value, ValueFormatContext context) => value;
}

public sealed class BooleanValueCodec : IValueCodec<bool>
{
    public bool Parse(ValueLiteral literal, ValueParseContext context)
    {
        if (literal.RawValue is bool boolean)
        {
            return boolean;
        }

        string text = SurfaceValue.Text(literal);
        if (bool.TryParse(text, out bool parsed))
        {
            return parsed;
        }
        if (text.Equals("yes", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (text.Equals("no", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        throw new FormatException($"'{text}' is not a boolean.");
    }

    public string Format(bool value, ValueFormatContext context) =>
        value ? "true" : "false";
}

public sealed class NumberValueCodec : IValueCodec<decimal>
{
    public decimal Parse(ValueLiteral literal, ValueParseContext context)
    {
        if (literal.RawValue is decimal number)
        {
            return number;
        }

        string text = SurfaceValue.Text(literal);
        return decimal.TryParse(
            text,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out decimal parsed)
            ? parsed
            : throw new FormatException($"'{text}' is not a number.");
    }

    public string Format(decimal value, ValueFormatContext context) =>
        value.ToString(CultureInfo.InvariantCulture);
}

public sealed class FileValueCodec : IValueCodec<FileInfo>
{
    public FileInfo Parse(ValueLiteral literal, ValueParseContext context) =>
        new(SurfaceValue.Text(literal));

    public string Format(FileInfo value, ValueFormatContext context) => value.FullName;
}

public sealed class DirectoryValueCodec : IValueCodec<DirectoryInfo>
{
    public DirectoryInfo Parse(ValueLiteral literal, ValueParseContext context) =>
        new(SurfaceValue.Text(literal));

    public string Format(DirectoryInfo value, ValueFormatContext context) => value.FullName;
}

public sealed class UriValueCodec : IValueCodec<Uri>
{
    public Uri Parse(ValueLiteral literal, ValueParseContext context)
    {
        string text = SurfaceValue.Text(literal);
        return Uri.TryCreate(text, UriKind.Absolute, out Uri? uri)
            ? uri
            : throw new FormatException($"'{text}' is not an absolute URI.");
    }

    public string Format(Uri value, ValueFormatContext context) => value.ToString();
}

public sealed class JsonElementValueCodec : IValueCodec<JsonElement>
{
    public JsonElement Parse(ValueLiteral literal, ValueParseContext context)
    {
        if (literal.RawValue is JsonElement element)
        {
            return element.Clone();
        }

        string json = literal.RawValue switch
        {
            string text => text,
            string[] lines => string.Join('\n', lines),
            _ => JsonSerializer.Serialize(literal.RawValue)
        };
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    public string Format(JsonElement value, ValueFormatContext context) => value.GetRawText();
}

public sealed class EncodingValueCodec : IValueCodec<Encoding>
{
    public Encoding Parse(ValueLiteral literal, ValueParseContext context)
    {
        string text = SurfaceValue.Text(literal);
        return text.ToUpperInvariant() switch
        {
            "UTF8" or "UTF-8" => Encoding.UTF8,
            "UTF32" or "UTF-32" => Encoding.UTF32,
            "ASCII" => Encoding.ASCII,
            "UNICODE" => Encoding.Unicode,
            _ => Encoding.GetEncoding(text)
        };
    }

    public string Format(Encoding value, ValueFormatContext context) => value.WebName;
}

public sealed class TextToFileConversion : IValueConversion<string, FileInfo>
{
    public FileInfo Convert(string value, ValueConversionContext context) => new(value);
}

public sealed class TextToDirectoryConversion : IValueConversion<string, DirectoryInfo>
{
    public DirectoryInfo Convert(string value, ValueConversionContext context) => new(value);
}

public sealed class TextToUriConversion : IValueConversion<string, Uri>
{
    public Uri Convert(string value, ValueConversionContext context) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            ? uri
            : throw new FormatException($"'{value}' is not an absolute URI.");
}

public sealed class TextToNumberConversion : IValueConversion<string, decimal>
{
    public decimal Convert(string value, ValueConversionContext context) =>
        decimal.TryParse(
            value,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out decimal parsed)
            ? parsed
            : throw new FormatException($"'{value}' is not a number.");
}

public sealed class TextToBooleanConversion : IValueConversion<string, bool>
{
    public bool Convert(string value, ValueConversionContext context) =>
        new BooleanValueCodec().Parse(
            new ValueLiteral(value),
            new ValueParseContext(context.Language));
}

public sealed class TextToJsonConversion : IValueConversion<string, JsonElement>
{
    public JsonElement Convert(string value, ValueConversionContext context) =>
        new JsonElementValueCodec().Parse(
            new ValueLiteral(value),
            new ValueParseContext(context.Language));
}

public sealed class TextToEncodingConversion : IValueConversion<string, Encoding>
{
    public Encoding Convert(string value, ValueConversionContext context) =>
        new EncodingValueCodec().Parse(
            new ValueLiteral(value),
            new ValueParseContext(context.Language));
}

public sealed class NumberToTextConversion : IValueConversion<decimal, string>
{
    public string Convert(decimal value, ValueConversionContext context) =>
        value.ToString(CultureInfo.InvariantCulture);
}

public sealed class BooleanToTextConversion : IValueConversion<bool, string>
{
    public string Convert(bool value, ValueConversionContext context) =>
        value ? "true" : "false";
}

public sealed class FileToTextConversion : IValueConversion<FileInfo, string>
{
    public string Convert(FileInfo value, ValueConversionContext context) => value.FullName;
}

public sealed class DirectoryToTextConversion : IValueConversion<DirectoryInfo, string>
{
    public string Convert(DirectoryInfo value, ValueConversionContext context) => value.FullName;
}

public sealed class UriToTextConversion : IValueConversion<Uri, string>
{
    public string Convert(Uri value, ValueConversionContext context) => value.ToString();
}

public sealed class JsonToTextConversion : IValueConversion<JsonElement, string>
{
    public string Convert(JsonElement value, ValueConversionContext context) => value.GetRawText();
}

public sealed class EncodingToTextConversion : IValueConversion<Encoding, string>
{
    public string Convert(Encoding value, ValueConversionContext context) => value.WebName;
}
