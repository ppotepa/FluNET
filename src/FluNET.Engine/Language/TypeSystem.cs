using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;

namespace FluNET.Language;

/// <summary>Structural category of a FluNET language type.</summary>
public enum TypeKind
{
    Scalar,
    Object,
    List,
    Map,
    Union
}

/// <summary>Whether a language value may be absent/null.</summary>
public enum TypeNullability
{
    NonNullable,
    Nullable
}

/// <summary>Stable identifiers of types owned by the FluNET core language.</summary>
public static class BuiltInTypeIds
{
    public static TypeId Unit { get; } = new("flunet.unit");
    public static TypeId Text { get; } = new("flunet.text");
    public static TypeId Boolean { get; } = new("flunet.boolean");
    public static TypeId Number { get; } = new("flunet.number");
    public static TypeId File { get; } = new("flunet.file");
    public static TypeId Directory { get; } = new("flunet.directory");
    public static TypeId Uri { get; } = new("flunet.uri");
    public static TypeId Json { get; } = new("flunet.json");
    public static TypeId Object { get; } = new("flunet.object");

    /// <summary>Encoding type used by the TRANSFORM command.</summary>
    public static TypeId Encoding { get; } = new("flunet.encoding");
}

/// <summary>One named field of a structured object type.</summary>
public sealed record TypeFieldSymbol
{
    public TypeFieldSymbol(string name, TypeSymbol type, bool isRequired = true)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("A type field needs a name.", nameof(name))
            : name.Trim();
        Type = type ?? throw new ArgumentNullException(nameof(type));
        IsRequired = isRequired;
    }

    public string Name { get; }
    public TypeSymbol Type { get; }
    public bool IsRequired { get; }
}

/// <summary>
/// Language-level type identity. Equality and assignability are defined from
/// stable FluNET metadata; CLR types are runtime mappings only.
/// </summary>
public sealed class TypeSymbol : IEquatable<TypeSymbol>
{
    private readonly object _runtimeGate = new();
    private readonly HashSet<Type> _runtimeTypes = [];
    private Type? _primaryRuntimeType;
    private readonly ReadOnlyDictionary<string, TypeFieldSymbol> _fields;
    private readonly ReadOnlyCollection<TypeSymbol> _unionTypes;
    private readonly TypeSymbol? _nonNullableType;

    internal TypeSymbol(
        TypeId id,
        string name,
        TypeKind kind,
        TypeNullability nullability,
        IEnumerable<Type>? runtimeTypes = null,
        TypeSymbol? elementType = null,
        TypeSymbol? keyType = null,
        TypeSymbol? valueType = null,
        IEnumerable<TypeFieldSymbol>? fields = null,
        IEnumerable<TypeSymbol>? unionTypes = null,
        TypeSymbol? nonNullableType = null)
    {
        Id = id.IsEmpty
            ? throw new ArgumentException("A type id is required.", nameof(id))
            : id;
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("A type symbol needs a name.", nameof(name))
            : name.Trim();
        Kind = kind;
        Nullability = nullability;
        ElementType = elementType;
        KeyType = keyType;
        ValueType = valueType;
        _nonNullableType = nonNullableType;

        Dictionary<string, TypeFieldSymbol> fieldIndex = new(StringComparer.OrdinalIgnoreCase);
        foreach (TypeFieldSymbol field in fields ?? Array.Empty<TypeFieldSymbol>())
        {
            if (!fieldIndex.TryAdd(field.Name, field))
            {
                throw new LanguageDefinitionException(
                    $"Object type '{Name}' declares field '{field.Name}' more than once.");
            }
        }
        _fields = new ReadOnlyDictionary<string, TypeFieldSymbol>(fieldIndex);

        TypeSymbol[] members = (unionTypes ?? Array.Empty<TypeSymbol>())
            .DistinctBy(member => member.Id)
            .OrderBy(member => member.Id.Value, StringComparer.Ordinal)
            .ToArray();
        _unionTypes = Array.AsReadOnly(members);

        foreach (Type runtimeType in runtimeTypes ?? Array.Empty<Type>())
        {
            AddRuntimeType(runtimeType);
        }
    }

    public TypeId Id { get; }
    public string Name { get; }
    public TypeKind Kind { get; }
    public TypeNullability Nullability { get; }

    /// <summary>
    /// Primary CLR runtime mapping. It is not part of language identity and may
    /// be null for structural-only types such as unions.
    /// </summary>
    public Type? ClrType
    {
        get
        {
            lock (_runtimeGate)
            {
                return _primaryRuntimeType;
            }
        }
    }

    /// <summary>All CLR representations known to map to this language type.</summary>
    public IReadOnlyCollection<Type> RuntimeTypes
    {
        get
        {
            lock (_runtimeGate)
            {
                return _runtimeTypes.ToArray();
            }
        }
    }

    public TypeSymbol? ElementType { get; }
    public TypeSymbol? KeyType { get; }
    public TypeSymbol? ValueType { get; }
    public IReadOnlyDictionary<string, TypeFieldSymbol> Fields => _fields;
    public IReadOnlyList<TypeSymbol> UnionTypes => _unionTypes;
    public bool IsCollection => Kind is TypeKind.List or TypeKind.Map;
    public bool IsNullable => Nullability == TypeNullability.Nullable;
    public bool IsOptional => IsNullable;

    /// <summary>The non-nullable type wrapped by Optional&lt;T&gt;, or this symbol.</summary>
    public TypeSymbol NonNullableType => _nonNullableType ?? this;

    /// <summary>Tests language assignability without consulting CLR inheritance or conversions.</summary>
    public bool IsAssignableFrom(TypeSymbol source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (Equals(source))
        {
            return true;
        }

        if (IsNullable)
        {
            return NonNullableType.IsAssignableFrom(source.NonNullableType);
        }
        if (source.IsNullable)
        {
            return false;
        }

        if (Kind == TypeKind.Union)
        {
            return source.Kind == TypeKind.Union
                ? source.UnionTypes.All(IsAssignableFrom)
                : UnionTypes.Any(member => member.IsAssignableFrom(source));
        }
        if (source.Kind == TypeKind.Union)
        {
            return source.UnionTypes.All(IsAssignableFrom);
        }

        if (Kind == TypeKind.List && source.Kind == TypeKind.List)
        {
            return ElementType is not null &&
                source.ElementType is not null &&
                ElementType.IsAssignableFrom(source.ElementType);
        }

        if (Kind == TypeKind.Map && source.Kind == TypeKind.Map)
        {
            return KeyType is not null &&
                source.KeyType is not null &&
                ValueType is not null &&
                source.ValueType is not null &&
                KeyType.Id == source.KeyType.Id &&
                ValueType.IsAssignableFrom(source.ValueType);
        }

        if (Kind == TypeKind.Object && source.Kind == TypeKind.Object && Fields.Count > 0)
        {
            foreach (TypeFieldSymbol targetField in Fields.Values)
            {
                if (!source.Fields.TryGetValue(targetField.Name, out TypeFieldSymbol? sourceField))
                {
                    if (targetField.IsRequired)
                    {
                        return false;
                    }
                    continue;
                }

                if (!targetField.Type.IsAssignableFrom(sourceField.Type))
                {
                    return false;
                }
            }
            return true;
        }

        return false;
    }

    internal void AddRuntimeType(Type runtimeType)
    {
        ArgumentNullException.ThrowIfNull(runtimeType);
        lock (_runtimeGate)
        {
            if (_runtimeTypes.Add(runtimeType) && _primaryRuntimeType is null)
            {
                _primaryRuntimeType = runtimeType;
            }
        }
    }

    public bool Equals(TypeSymbol? other) => other is not null && Id == other.Id;
    public override bool Equals(object? obj) => obj is TypeSymbol other && Equals(other);
    public override int GetHashCode() => Id.GetHashCode();
    public override string ToString() => Name;
}

/// <summary>
/// Canonical catalog of language types for one language snapshot. Structural
/// types are interned by TypeId, while CLR mappings are kept in a separate index.
/// </summary>
public sealed class LanguageTypeSystem
{
    private static readonly Type[] NumericRuntimeTypes =
    [
        typeof(decimal), typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
        typeof(int), typeof(uint), typeof(long), typeof(ulong),
        typeof(float), typeof(double)
    ];

    private readonly object _gate = new();
    private readonly Dictionary<Type, TypeSymbol> _byClrType = [];
    private readonly Dictionary<TypeId, TypeSymbol> _byId = [];
    private readonly Dictionary<string, TypeSymbol> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyDictionary<Type, string> _customNames;

    internal LanguageTypeSystem(
        IEnumerable<Type> requiredTypes,
        IReadOnlyDictionary<Type, string> customNames)
    {
        ArgumentNullException.ThrowIfNull(requiredTypes);
        _customNames = customNames ?? throw new ArgumentNullException(nameof(customNames));

        RegisterBuiltIns();

        foreach (Type type in requiredTypes.Concat(customNames.Keys).Distinct())
        {
            AddClrType(type, allowUnregisteredScalar: true);
        }
    }

    public TypeSymbol Unit => Get(typeof(void));
    public TypeSymbol Text => Get(typeof(string));
    public TypeSymbol Boolean => Get(typeof(bool));
    public TypeSymbol Number => Get(typeof(decimal));
    public TypeSymbol File => Get(typeof(FileInfo));
    public TypeSymbol Directory => Get(typeof(DirectoryInfo));
    public TypeSymbol Uri => Get(typeof(Uri));
    public TypeSymbol Json => Get(typeof(JsonElement));
    public TypeSymbol Object => Get(typeof(Dictionary<string, object>));

    public IReadOnlyCollection<TypeSymbol> Symbols
    {
        get
        {
            lock (_gate)
            {
                return _byId.Values
                    .OrderBy(symbol => symbol.Id.Value, StringComparer.Ordinal)
                    .ToArray();
            }
        }
    }

    public TypeSymbol Get(Type clrType)
    {
        ArgumentNullException.ThrowIfNull(clrType);
        lock (_gate)
        {
            if (_byClrType.TryGetValue(clrType, out TypeSymbol? symbol))
            {
                ValidateCustomName(clrType, symbol);
                return symbol;
            }
            return AddClrType(clrType, allowUnregisteredScalar: false);
        }
    }

    public TypeSymbol Get<T>() => Get(typeof(T));

    public TypeSymbol? Find(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (_gate)
        {
            return _byName.TryGetValue(name, out TypeSymbol? symbol) ? symbol : null;
        }
    }

    public TypeSymbol? Find(TypeId id)
    {
        if (id.IsEmpty)
        {
            return null;
        }
        lock (_gate)
        {
            return _byId.TryGetValue(id, out TypeSymbol? symbol) ? symbol : null;
        }
    }

    /// <summary>Returns the canonical List&lt;T&gt; symbol for an element type.</summary>
    public TypeSymbol List(TypeSymbol elementType)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        lock (_gate)
        {
            return Intern(
                new TypeId($"list<{elementType.Id.Value}>"),
                $"List<{elementType.Name}>",
                TypeKind.List,
                TypeNullability.NonNullable,
                elementType: elementType);
        }
    }

    /// <summary>Returns the canonical Map&lt;K,V&gt; symbol.</summary>
    public TypeSymbol Map(TypeSymbol keyType, TypeSymbol valueType)
    {
        ArgumentNullException.ThrowIfNull(keyType);
        ArgumentNullException.ThrowIfNull(valueType);
        lock (_gate)
        {
            return Intern(
                new TypeId($"map<{keyType.Id.Value},{valueType.Id.Value}>"),
                $"Map<{keyType.Name},{valueType.Name}>",
                TypeKind.Map,
                TypeNullability.NonNullable,
                keyType: keyType,
                valueType: valueType);
        }
    }

    /// <summary>Returns the canonical Optional&lt;T&gt; symbol.</summary>
    public TypeSymbol Optional(TypeSymbol valueType)
    {
        ArgumentNullException.ThrowIfNull(valueType);
        if (valueType.IsNullable)
        {
            return valueType;
        }

        lock (_gate)
        {
            return Intern(
                new TypeId($"optional<{valueType.Id.Value}>"),
                $"Optional<{valueType.Name}>",
                valueType.Kind,
                TypeNullability.Nullable,
                elementType: valueType.ElementType,
                keyType: valueType.KeyType,
                valueType: valueType.ValueType,
                fields: valueType.Fields.Values,
                unionTypes: valueType.UnionTypes,
                nonNullableType: valueType);
        }
    }

    /// <summary>Returns a canonical union with flattened, de-duplicated members.</summary>
    public TypeSymbol Union(params TypeSymbol[] types) => Union((IEnumerable<TypeSymbol>)types);

    public TypeSymbol Union(IEnumerable<TypeSymbol> types)
    {
        ArgumentNullException.ThrowIfNull(types);
        TypeSymbol[] members = types
            .SelectMany(type => type.Kind == TypeKind.Union && !type.IsNullable
                ? type.UnionTypes
                : [type])
            .DistinctBy(type => type.Id)
            .OrderBy(type => type.Id.Value, StringComparer.Ordinal)
            .ToArray();
        if (members.Length == 0)
        {
            throw new ArgumentException("A union must contain at least one type.", nameof(types));
        }
        if (members.Length == 1)
        {
            return members[0];
        }

        lock (_gate)
        {
            string ids = string.Join('|', members.Select(type => type.Id.Value));
            string names = string.Join(" | ", members.Select(type => type.Name));
            return Intern(
                new TypeId($"union<{ids}>"),
                names,
                TypeKind.Union,
                TypeNullability.NonNullable,
                unionTypes: members);
        }
    }

    /// <summary>Declares/interns a structured object type with named fields.</summary>
    public TypeSymbol ObjectType(
        TypeId id,
        string name,
        IEnumerable<TypeFieldSymbol> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        lock (_gate)
        {
            return Intern(
                id,
                name,
                TypeKind.Object,
                TypeNullability.NonNullable,
                fields: fields);
        }
    }

    private void RegisterBuiltIns()
    {
        Intern(BuiltInTypeIds.Unit, "Unit", TypeKind.Scalar, TypeNullability.NonNullable,
            runtimeTypes: [typeof(void)]);
        Intern(BuiltInTypeIds.Text, "Text", TypeKind.Scalar, TypeNullability.NonNullable,
            runtimeTypes: [typeof(string)]);
        Intern(BuiltInTypeIds.Boolean, "Boolean", TypeKind.Scalar, TypeNullability.NonNullable,
            runtimeTypes: [typeof(bool)]);
        Intern(BuiltInTypeIds.Number, "Number", TypeKind.Scalar, TypeNullability.NonNullable,
            runtimeTypes: NumericRuntimeTypes);
        Intern(BuiltInTypeIds.File, "File", TypeKind.Scalar, TypeNullability.NonNullable,
            runtimeTypes: [typeof(FileInfo)]);
        Intern(BuiltInTypeIds.Directory, "Directory", TypeKind.Scalar, TypeNullability.NonNullable,
            runtimeTypes: [typeof(DirectoryInfo)]);
        Intern(BuiltInTypeIds.Uri, "Uri", TypeKind.Scalar, TypeNullability.NonNullable,
            runtimeTypes: [typeof(Uri)]);
        Intern(BuiltInTypeIds.Json, "Json", TypeKind.Scalar, TypeNullability.NonNullable,
            runtimeTypes: [typeof(JsonElement)]);
        Intern(BuiltInTypeIds.Object, "Object", TypeKind.Object, TypeNullability.NonNullable,
            runtimeTypes: [typeof(Dictionary<string, object>)]);
        Intern(BuiltInTypeIds.Encoding, "System.Text.Encoding", TypeKind.Scalar,
            TypeNullability.NonNullable, runtimeTypes: [typeof(Encoding)]);
    }

    private TypeSymbol AddClrType(Type type, bool allowUnregisteredScalar)
    {
        if (_byClrType.TryGetValue(type, out TypeSymbol? existing))
        {
            ValidateCustomName(type, existing);
            return existing;
        }

        Type? nullableValueType = Nullable.GetUnderlyingType(type);
        if (nullableValueType is not null)
        {
            TypeSymbol valueType = AddClrType(nullableValueType, allowUnregisteredScalar);
            TypeSymbol optional = Optional(valueType);
            MapRuntimeType(type, optional);
            return optional;
        }

        if (TryFindMapArguments(type, out Type keyClrType, out Type valueClrType))
        {
            TypeSymbol key = AddClrType(keyClrType, allowUnregisteredScalar);
            TypeSymbol value = AddClrType(valueClrType, allowUnregisteredScalar);
            TypeSymbol map = Map(key, value);
            MapRuntimeType(type, map);
            return map;
        }

        Type? elementClrType = FindListElementType(type);
        if (elementClrType is not null)
        {
            TypeSymbol element = AddClrType(elementClrType, allowUnregisteredScalar);
            TypeSymbol list = List(element);
            MapRuntimeType(type, list);
            return list;
        }

        if (_customNames.TryGetValue(type, out string? customName))
        {
            TypeId id = new($"type.{NormalizeTypeName(customName)}");
            return Intern(
                id,
                customName,
                TypeKind.Scalar,
                TypeNullability.NonNullable,
                runtimeTypes: [type]);
        }

        if (!allowUnregisteredScalar)
        {
            throw new LanguageDefinitionException(
                $"CLR type '{type}' is not part of this language snapshot. " +
                "Register custom types with LanguageBuilder.Type<T>(name).");
        }

        TypeId clrTypeId = new($"clr.{NormalizeClrIdentity(type)}");
        return Intern(
            clrTypeId,
            type.FullName ?? type.Name,
            TypeKind.Scalar,
            TypeNullability.NonNullable,
            runtimeTypes: [type]);
    }

    private void ValidateCustomName(Type runtimeType, TypeSymbol symbol)
    {
        if (!_customNames.TryGetValue(runtimeType, out string? customName))
        {
            return;
        }

        if (IsBuiltInId(symbol.Id) && customName.Equals(symbol.Name, StringComparison.Ordinal))
        {
            return;
        }

        TypeId expected = new($"type.{NormalizeTypeName(customName)}");
        if (symbol.Id != expected || !symbol.Name.Equals(customName, StringComparison.Ordinal))
        {
            throw new LanguageDefinitionException(
                $"CLR type '{runtimeType}' already maps to built-in language type " +
                $"'{symbol.Id}'/'{symbol.Name}' and cannot be renamed to '{customName}'.");
        }
    }

    private static bool IsBuiltInId(TypeId id) =>
        id == BuiltInTypeIds.Unit ||
        id == BuiltInTypeIds.Text ||
        id == BuiltInTypeIds.Boolean ||
        id == BuiltInTypeIds.Number ||
        id == BuiltInTypeIds.File ||
        id == BuiltInTypeIds.Directory ||
        id == BuiltInTypeIds.Uri ||
        id == BuiltInTypeIds.Json ||
        id == BuiltInTypeIds.Object ||
        id == BuiltInTypeIds.Encoding;

    private TypeSymbol Intern(
        TypeId id,
        string name,
        TypeKind kind,
        TypeNullability nullability,
        IEnumerable<Type>? runtimeTypes = null,
        TypeSymbol? elementType = null,
        TypeSymbol? keyType = null,
        TypeSymbol? valueType = null,
        IEnumerable<TypeFieldSymbol>? fields = null,
        IEnumerable<TypeSymbol>? unionTypes = null,
        TypeSymbol? nonNullableType = null)
    {
        if (id.IsEmpty)
        {
            throw new ArgumentException("A type id is required.", nameof(id));
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A type name is required.", nameof(name));
        }

        TypeFieldSymbol[] fieldSnapshot = fields?.ToArray() ?? Array.Empty<TypeFieldSymbol>();
        TypeSymbol[] unionSnapshot = unionTypes?.ToArray() ?? Array.Empty<TypeSymbol>();
        Type[] runtimeSnapshot = runtimeTypes?.ToArray() ?? Array.Empty<Type>();

        if (_byId.TryGetValue(id, out TypeSymbol? existing))
        {
            if (!SameShape(
                existing,
                name,
                kind,
                nullability,
                elementType,
                keyType,
                valueType,
                fieldSnapshot,
                unionSnapshot,
                nonNullableType))
            {
                throw new LanguageDefinitionException(
                    $"Type id '{id}' is already registered with a different structure.");
            }
            foreach (Type runtimeType in runtimeSnapshot)
            {
                MapRuntimeType(runtimeType, existing);
            }
            return existing;
        }

        if (_byName.TryGetValue(name, out TypeSymbol? nameCollision) && nameCollision.Id != id)
        {
            throw new LanguageDefinitionException(
                $"Language type name '{name}' belongs to both '{nameCollision.Id}' and '{id}'.");
        }

        TypeSymbol symbol = new(
            id,
            name,
            kind,
            nullability,
            runtimeSnapshot,
            elementType,
            keyType,
            valueType,
            fieldSnapshot,
            unionSnapshot,
            nonNullableType);
        _byId.Add(id, symbol);
        _byName[name] = symbol;
        foreach (Type runtimeType in runtimeSnapshot)
        {
            MapRuntimeType(runtimeType, symbol);
        }
        return symbol;
    }

    private void MapRuntimeType(Type runtimeType, TypeSymbol symbol)
    {
        if (_byClrType.TryGetValue(runtimeType, out TypeSymbol? collision) && collision.Id != symbol.Id)
        {
            throw new LanguageDefinitionException(
                $"CLR type '{runtimeType}' maps to both '{collision.Id}' and '{symbol.Id}'.");
        }
        _byClrType[runtimeType] = symbol;
        symbol.AddRuntimeType(runtimeType);
    }

    private static bool SameShape(
        TypeSymbol existing,
        string name,
        TypeKind kind,
        TypeNullability nullability,
        TypeSymbol? elementType,
        TypeSymbol? keyType,
        TypeSymbol? valueType,
        IReadOnlyList<TypeFieldSymbol> fields,
        IReadOnlyList<TypeSymbol> unionTypes,
        TypeSymbol? nonNullableType)
    {
        TypeId expectedNonNullable = nonNullableType?.Id ?? existing.Id;
        if (!existing.Name.Equals(name, StringComparison.Ordinal) ||
            existing.Kind != kind ||
            existing.Nullability != nullability ||
            existing.ElementType?.Id != elementType?.Id ||
            existing.KeyType?.Id != keyType?.Id ||
            existing.ValueType?.Id != valueType?.Id ||
            existing.NonNullableType.Id != expectedNonNullable)
        {
            return false;
        }

        if (existing.Fields.Count != fields.Count ||
            fields.Any(field =>
                !existing.Fields.TryGetValue(field.Name, out TypeFieldSymbol? current) ||
                current.Type.Id != field.Type.Id ||
                current.IsRequired != field.IsRequired))
        {
            return false;
        }

        TypeId[] existingMembers = existing.UnionTypes
            .Select(type => type.Id)
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .ToArray();
        TypeId[] incomingMembers = unionTypes
            .Select(type => type.Id)
            .Distinct()
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .ToArray();
        return existingMembers.SequenceEqual(incomingMembers);
    }

    private static bool TryFindMapArguments(Type type, out Type keyType, out Type valueType)
    {
        Type? candidate = EnumerateSelfAndInterfaces(type).FirstOrDefault(item =>
            item.IsGenericType && IsMapDefinition(item.GetGenericTypeDefinition()));
        if (candidate is null)
        {
            keyType = null!;
            valueType = null!;
            return false;
        }

        keyType = candidate.GenericTypeArguments[0];
        valueType = candidate.GenericTypeArguments[1];
        return true;
    }

    private static Type? FindListElementType(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType();
        }

        Type? candidate = EnumerateSelfAndInterfaces(type).FirstOrDefault(item =>
            item.IsGenericType && IsListDefinition(item.GetGenericTypeDefinition()));
        return candidate?.GenericTypeArguments[0];
    }

    private static bool IsMapDefinition(Type definition) =>
        definition == typeof(IDictionary<,>) ||
        definition == typeof(IReadOnlyDictionary<,>) ||
        definition == typeof(Dictionary<,>);

    private static bool IsListDefinition(Type definition) =>
        definition == typeof(List<>) ||
        definition == typeof(IList<>) ||
        definition == typeof(IReadOnlyList<>) ||
        definition == typeof(ICollection<>) ||
        definition == typeof(IReadOnlyCollection<>);

    private static IEnumerable<Type> EnumerateSelfAndInterfaces(Type type) =>
        new[] { type }.Concat(type.GetInterfaces());

    private static string NormalizeTypeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new LanguageDefinitionException("A custom language type name cannot be empty.");
        }
        return new string(name.Trim().ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_'
                ? character
                : '-')
            .ToArray());
    }

    private static string NormalizeClrIdentity(Type type)
    {
        string value = type.FullName ?? type.Name;
        return value.Replace(' ', '-').ToLowerInvariant();
    }
}

/// <summary>A named value and its inferred or declared type in a compiled program.</summary>
public sealed record VariableSymbol(string Name, TypeSymbol Type, int ProducerIndex);
