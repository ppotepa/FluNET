using System.Security.Cryptography;
using System.Text.Json;

namespace FluNET.Declarative.Reconciliation;

public sealed record ResourceIdentity
{
    public ResourceIdentity(string scheme, string value)
    {
        if (string.IsNullOrWhiteSpace(scheme)) throw new ArgumentException("Resource scheme is required.", nameof(scheme));
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Resource identity value is required.", nameof(value));
        Scheme = scheme.Trim().ToLowerInvariant();
        Value = value.Trim();
    }

    public string Scheme { get; init; }
    public string Value { get; init; }

    public static ResourceIdentity Parse(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        string text = source.Trim();
        if (Uri.TryCreate(text, UriKind.Absolute, out Uri? uri)) return new(uri.Scheme, uri.ToString());
        int colon = text.IndexOf(':');
        return colon > 0 ? new(text[..colon], text[(colon + 1)..]) : new("file", Path.GetFullPath(text));
    }

    public override string ToString() => $"{Scheme}:{Value}";
}

public sealed record StateRecord(string Key, JsonElement Value, string Fingerprint)
{
    public static StateRecord FromJson(JsonElement value, string keyField)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new FormatException("Reconciliation records must be JSON objects.");
        if (!TryGetProperty(value, keyField, out JsonElement key)) throw new FormatException($"Record does not contain identity field '{keyField}'.");
        string keyValue = KeyValue(key);
        return new(keyValue, value.Clone(), StateCanonicalizer.Fingerprint(value));
    }

    private static bool TryGetProperty(JsonElement value, string name, out JsonElement result)
    {
        if (value.TryGetProperty(name, out result)) return true;
        foreach (JsonProperty property in value.EnumerateObject())
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) { result = property.Value; return true; }
        result = default;
        return false;
    }

    private static string KeyValue(JsonElement key) => key.ValueKind switch
    {
        JsonValueKind.String => key.GetString() ?? string.Empty,
        JsonValueKind.Number => key.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => throw new FormatException("Reconciliation identity fields must be string, number or Boolean scalars.")
    };
}

public abstract record ResourceStateSnapshot(
    ResourceIdentity Identity,
    string KeyField,
    IReadOnlyList<StateRecord> Records,
    DateTimeOffset CapturedAt)
{
    protected static IReadOnlyList<StateRecord> BuildRecords(IEnumerable<JsonElement> values, string keyField)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyField);
        StateRecord[] records = values.Select(value => StateRecord.FromJson(value, keyField)).ToArray();
        string[] duplicates = records.GroupBy(record => record.Key, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        if (duplicates.Length > 0) throw new FormatException($"Duplicate reconciliation identities: {string.Join(", ", duplicates)}.");
        return records.OrderBy(record => record.Key, StringComparer.Ordinal).ToArray();
    }
}

public sealed record DesiredStateSnapshot : ResourceStateSnapshot
{
    public DesiredStateSnapshot(ResourceIdentity identity, string keyField, IEnumerable<JsonElement> values, DateTimeOffset? capturedAt = null)
        : base(identity, keyField, BuildRecords(values, keyField), capturedAt ?? DateTimeOffset.UtcNow) { }
}

public sealed record ObservedStateSnapshot : ResourceStateSnapshot
{
    public ObservedStateSnapshot(ResourceIdentity identity, string keyField, IEnumerable<JsonElement> values, DateTimeOffset? capturedAt = null)
        : base(identity, keyField, BuildRecords(values, keyField), capturedAt ?? DateTimeOffset.UtcNow) { }
}

public static class StateCanonicalizer
{
    public static string Fingerprint(JsonElement value)
    {
        byte[] canonical = CanonicalBytes(value);
        return Convert.ToHexString(SHA256.HashData(canonical)).ToLowerInvariant();
    }

    public static byte[] CanonicalBytes(JsonElement value)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = false })) Write(writer, value);
        return stream.ToArray();
    }

    private static void Write(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                { writer.WritePropertyName(property.Name); Write(writer, property.Value); }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in value.EnumerateArray()) Write(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String: writer.WriteStringValue(value.GetString()); break;
            case JsonValueKind.Number: writer.WriteRawValue(value.GetRawText(), skipInputValidation: true); break;
            case JsonValueKind.True: writer.WriteBooleanValue(true); break;
            case JsonValueKind.False: writer.WriteBooleanValue(false); break;
            case JsonValueKind.Null: writer.WriteNullValue(); break;
            default: writer.WriteRawValue(value.GetRawText(), skipInputValidation: true); break;
        }
    }
}
