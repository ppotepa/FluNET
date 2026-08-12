using System.Collections.ObjectModel;
using System.Text.Json;

namespace FluNET.Language;

/// <summary>A language-level type independent from a particular CLR spelling.</summary>
public sealed record TypeSymbol
{
    internal TypeSymbol(string name, Type clrType, TypeSymbol? elementType = null)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("A type symbol needs a name.", nameof(name))
            : name.Trim();
        ClrType = clrType ?? throw new ArgumentNullException(nameof(clrType));
        ElementType = elementType;
    }

    public string Name { get; }
    public Type ClrType { get; }
    public TypeSymbol? ElementType { get; }
    public bool IsCollection => ElementType is not null;

    public bool IsAssignableFrom(TypeSymbol source) =>
        ClrType.IsAssignableFrom(source.ClrType) ||
        (Name.Equals("Text", StringComparison.OrdinalIgnoreCase) && source.ClrType != typeof(void));

    public override string ToString() => Name;
}

/// <summary>Immutable catalog of value types understood by one language version.</summary>
public sealed class LanguageTypeSystem
{
    private readonly IReadOnlyDictionary<Type, TypeSymbol> _byClrType;
    private readonly IReadOnlyDictionary<string, TypeSymbol> _byName;

    internal LanguageTypeSystem(
        IEnumerable<Type> requiredTypes,
        IReadOnlyDictionary<Type, string> customNames)
    {
        Dictionary<Type, TypeSymbol> byClr = [];
        Dictionary<string, TypeSymbol> byName = new(StringComparer.OrdinalIgnoreCase);
        foreach (Type type in requiredTypes.Concat(customNames.Keys).Append(typeof(void)).Distinct())
        {
            Add(type, byClr, byName, customNames);
        }

        _byClrType = new ReadOnlyDictionary<Type, TypeSymbol>(byClr);
        _byName = new ReadOnlyDictionary<string, TypeSymbol>(byName);
    }

    public IReadOnlyCollection<TypeSymbol> Symbols => _byClrType.Values
        .Distinct()
        .OrderBy(symbol => symbol.Name, StringComparer.Ordinal)
        .ToArray();

    public TypeSymbol Get(Type clrType) =>
        _byClrType.TryGetValue(clrType, out TypeSymbol? symbol)
            ? symbol
            : throw new LanguageDefinitionException($"CLR type '{clrType}' is not part of this language snapshot.");

    public TypeSymbol Get<T>() => Get(typeof(T));

    public TypeSymbol? Find(string name) =>
        _byName.TryGetValue(name, out TypeSymbol? symbol) ? symbol : null;

    private static TypeSymbol Add(
        Type type,
        IDictionary<Type, TypeSymbol> byClr,
        IDictionary<string, TypeSymbol> byName,
        IReadOnlyDictionary<Type, string> customNames)
    {
        if (byClr.TryGetValue(type, out TypeSymbol? existing))
        {
            return existing;
        }

        Type? elementClrType = FindElementType(type);
        TypeSymbol? element = elementClrType is null
            ? null
            : Add(elementClrType, byClr, byName, customNames);
        string name = customNames.TryGetValue(type, out string? custom)
            ? custom
            : DefaultName(type, element);
        TypeSymbol symbol = new(name, type, element);
        if (byName.TryGetValue(name, out TypeSymbol? collision) && collision.ClrType != type)
        {
            throw new LanguageDefinitionException(
                $"Language type name '{name}' belongs to both '{collision.ClrType}' and '{type}'.");
        }

        byClr.Add(type, symbol);
        byName[name] = symbol;
        return symbol;
    }

    private static Type? FindElementType(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType();
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            return type.GenericTypeArguments[0];
        }
        return null;
    }

    private static string DefaultName(Type type, TypeSymbol? element) => type switch
    {
        _ when type == typeof(void) => "Unit",
        _ when type == typeof(string) => "Text",
        _ when type == typeof(bool) => "Boolean",
        _ when type == typeof(byte) || type == typeof(short) || type == typeof(int) ||
            type == typeof(long) || type == typeof(float) || type == typeof(double) ||
            type == typeof(decimal) => type.Name,
        _ when type == typeof(Uri) => "Uri",
        _ when type == typeof(FileInfo) => "File",
        _ when type == typeof(DirectoryInfo) => "Directory",
        _ when type == typeof(JsonElement) => "Json",
        _ when type == typeof(Dictionary<string, object>) => "Object",
        _ when element is not null => $"List<{element.Name}>",
        _ => type.FullName ?? type.Name
    };
}

/// <summary>A named value and its inferred or declared type in a compiled program.</summary>
public sealed record VariableSymbol(string Name, TypeSymbol Type, int ProducerIndex);
