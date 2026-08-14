using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace FluNET.Language.Resources;

/// <summary>RFC4180-style comma separated records normalized to the existing List&lt;Json&gt; runtime representation.</summary>
public sealed class CsvResourceDecoder : IResourceDecoder
{
    public string Id => "surface.decoder.csv";
    public bool CanDecode(ResourceDescriptor descriptor, ResourcePayload payload) => descriptor.Format == ResourceFormat.Csv;

    public object Decode(ResourceDescriptor descriptor, ResourcePayload payload)
    {
        string text = Encoding.UTF8.GetString(payload.Content.Span);
        List<string[]> rows = ParseRows(text);
        if (rows.Count == 0) return Array.Empty<JsonElement>();
        string[] headers = rows[0].Select((header, index) => string.IsNullOrWhiteSpace(header) ? $"column{index + 1}" : header.Trim()).ToArray();
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
        {
            string original = headers[i];
            string candidate = original;
            int suffix = 2;
            while (!names.Add(candidate)) candidate = $"{original}_{suffix++}";
            headers[i] = candidate;
        }

        List<JsonElement> result = [];
        foreach (string[] row in rows.Skip(1))
        {
            if (row.All(string.IsNullOrEmpty)) continue;
            Dictionary<string, object?> record = new(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Length; i++)
                record[headers[i]] = i < row.Length ? Scalar(row[i]) : null;
            result.Add(JsonSerializer.SerializeToElement(record));
        }
        return result.ToArray();
    }

    private static object? Scalar(string value)
    {
        string text = value.Trim();
        if (text.Length == 0) return string.Empty;
        if (bool.TryParse(text, out bool boolean)) return boolean;
        if (decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out decimal number)) return number;
        return value;
    }

    private static List<string[]> ParseRows(string source)
    {
        List<string[]> rows = [];
        List<string> row = [];
        StringBuilder field = new();
        bool quoted = false;
        for (int i = 0; i <= source.Length; i++)
        {
            bool end = i == source.Length;
            char ch = end ? '\n' : source[i];
            if (quoted)
            {
                if (ch == '"')
                {
                    if (i + 1 < source.Length && source[i + 1] == '"') { field.Append('"'); i++; }
                    else quoted = false;
                }
                else field.Append(ch);
                continue;
            }
            if (ch == '"' && field.Length == 0) { quoted = true; continue; }
            if (ch == ',') { row.Add(field.ToString()); field.Clear(); continue; }
            if (ch is '\r' or '\n')
            {
                if (ch == '\r' && i + 1 < source.Length && source[i + 1] == '\n') i++;
                row.Add(field.ToString()); field.Clear();
                if (row.Count > 1 || row[0].Length > 0) rows.Add(row.ToArray());
                row.Clear();
                continue;
            }
            field.Append(ch);
        }
        if (quoted) throw new FormatException("CSV contains an unterminated quoted field.");
        return rows;
    }
}

/// <summary>XML normalized to Json using @attribute, #text, and repeated-child arrays.</summary>
public sealed class XmlResourceDecoder : IResourceDecoder
{
    public string Id => "surface.decoder.xml";
    public bool CanDecode(ResourceDescriptor descriptor, ResourcePayload payload) => descriptor.Format == ResourceFormat.Xml;

    public object Decode(ResourceDescriptor descriptor, ResourcePayload payload)
    {
        using MemoryStream stream = new(payload.Content.ToArray(), writable: false);
        XDocument document = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        XElement root = document.Root ?? throw new FormatException("XML document has no root element.");
        Dictionary<string, object?> wrapper = new(StringComparer.Ordinal) { [root.Name.LocalName] = ConvertElement(root) };
        return JsonSerializer.SerializeToElement(wrapper);
    }

    private static object? ConvertElement(XElement element)
    {
        Dictionary<string, object?> result = new(StringComparer.Ordinal);
        foreach (XAttribute attribute in element.Attributes()) result[$"@{attribute.Name.LocalName}"] = attribute.Value;

        XElement[] children = element.Elements().ToArray();
        foreach (IGrouping<string, XElement> group in children.GroupBy(child => child.Name.LocalName, StringComparer.Ordinal))
        {
            object?[] values = group.Select(ConvertElement).ToArray();
            result[group.Key] = values.Length == 1 ? values[0] : values;
        }

        string text = string.Concat(element.Nodes().OfType<XText>().Select(node => node.Value)).Trim();
        if (text.Length > 0) result["#text"] = Scalar(text);
        if (result.Count == 0) return text;
        return result;
    }

    private static object Scalar(string value)
    {
        if (bool.TryParse(value, out bool boolean)) return boolean;
        if (decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out decimal number)) return number;
        return value;
    }
}
