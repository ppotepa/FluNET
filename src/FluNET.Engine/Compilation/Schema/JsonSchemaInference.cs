using FluNET.Language;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FluNET.Compilation.Schema;

public sealed record JsonSchemaInferenceResult(TypeSymbol Type, int SampleCount);

/// <summary>Infers structural language types from an explicit JSON sample without performing I/O.</summary>
public sealed class JsonSchemaInferencer
{
    public JsonSchemaInferenceResult Infer(
        IEnumerable<JsonElement> samples,
        LanguageTypeSystem types,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(types);
        JsonElement[] snapshot = samples.Select(item => item.Clone()).ToArray();
        if (snapshot.Length == 0)
            throw new ArgumentException("Schema inference requires at least one sample value.", nameof(samples));
        return new JsonSchemaInferenceResult(InferValues(snapshot, types, name), snapshot.Length);
    }

    private static TypeSymbol InferValues(
        IReadOnlyList<JsonElement> values,
        LanguageTypeSystem types,
        string? preferredName = null)
    {
        bool hasNull = values.Any(value => value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined);
        JsonElement[] nonNull = values
            .Where(value => value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
            .ToArray();
        if (nonNull.Length == 0) return types.Optional(types.Json);

        TypeSymbol[] inferred = nonNull
            .GroupBy(value => Category(value.ValueKind))
            .Select(group => InferCategory(group.ToArray(), types, preferredName))
            .DistinctBy(type => type.Id)
            .ToArray();
        TypeSymbol result = inferred.Length == 1 ? inferred[0] : types.Union(inferred);
        return hasNull ? types.Optional(result) : result;
    }

    private static TypeSymbol InferCategory(
        IReadOnlyList<JsonElement> values,
        LanguageTypeSystem types,
        string? preferredName)
    {
        JsonValueKind kind = values[0].ValueKind;
        return kind switch
        {
            JsonValueKind.String => types.Text,
            JsonValueKind.Number => types.Number,
            JsonValueKind.True or JsonValueKind.False => types.Boolean,
            JsonValueKind.Object => InferObject(values, types, preferredName),
            JsonValueKind.Array => InferArray(values, types),
            _ => types.Json
        };
    }

    private static TypeSymbol InferArray(IReadOnlyList<JsonElement> arrays, LanguageTypeSystem types)
    {
        JsonElement[] items = arrays
            .SelectMany(array => array.EnumerateArray())
            .Select(item => item.Clone())
            .ToArray();
        TypeSymbol element = items.Length == 0 ? types.Json : InferValues(items, types);
        return types.List(element);
    }

    private static TypeSymbol InferObject(
        IReadOnlyList<JsonElement> objects,
        LanguageTypeSystem types,
        string? preferredName)
    {
        string[] names = objects
            .SelectMany(value => value.EnumerateObject().Select(property => property.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        List<TypeFieldSymbol> fields = [];
        foreach (string fieldName in names)
        {
            List<JsonElement> fieldValues = [];
            int present = 0;
            bool hasNull = false;
            foreach (JsonElement value in objects)
            {
                if (!TryGetProperty(value, fieldName, out JsonElement property)) continue;
                present++;
                if (property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) hasNull = true;
                fieldValues.Add(property.Clone());
            }
            TypeSymbol fieldType = fieldValues.Count == 0 ? types.Json : InferValues(fieldValues, types);
            if (hasNull && !fieldType.IsNullable) fieldType = types.Optional(fieldType);
            bool required = present == objects.Count && !hasNull;
            fields.Add(new TypeFieldSymbol(fieldName, fieldType, required));
        }

        string canonical = string.Join(";", fields
            .OrderBy(field => field.Name, StringComparer.OrdinalIgnoreCase)
            .Select(field => $"{field.Name.ToLowerInvariant()}:{field.Type.Id.Value}:{field.IsRequired}"));
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant()[..20];
        TypeId id = new($"schema.{hash}");
        string name = string.IsNullOrWhiteSpace(preferredName) ? $"Object<{hash[..8]}>" : preferredName.Trim();
        return types.ObjectType(id, name, fields);
    }

    private static bool TryGetProperty(JsonElement value, string name, out JsonElement property)
    {
        if (value.TryGetProperty(name, out property)) return true;
        foreach (JsonProperty item in value.EnumerateObject())
        {
            if (item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                property = item.Value;
                return true;
            }
        }
        property = default;
        return false;
    }

    private static string Category(JsonValueKind kind) => kind switch
    {
        JsonValueKind.True or JsonValueKind.False => "boolean",
        JsonValueKind.Null or JsonValueKind.Undefined => "null",
        _ => kind.ToString()
    };
}

/// <summary>Host-owned declared/inferred schemas keyed by a stable logical name.</summary>
public sealed class JsonSchemaRegistry
{
    private readonly Dictionary<string, TypeSymbol> _schemas = new(StringComparer.OrdinalIgnoreCase);

    public void Declare(string name, TypeSymbol type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(type);
        if (_schemas.TryGetValue(name.Trim(), out TypeSymbol? existing) && existing.Id != type.Id)
            throw new LanguageDefinitionException($"Schema '{name}' is already declared as '{existing}'.");
        _schemas[name.Trim()] = type;
    }

    public bool TryGet(string name, out TypeSymbol? type) => _schemas.TryGetValue(name, out type);
    public IReadOnlyDictionary<string, TypeSymbol> Snapshot() => new Dictionary<string, TypeSymbol>(_schemas, StringComparer.OrdinalIgnoreCase);
}
