namespace FluNET.Language.Metadata;

public enum CollectionShapeKind
{
    Scalar,
    Array,
    Sequence
}

/// <summary>
/// CLR value-shape metadata. Collection shape is deliberately separate from
/// syntactic role cardinality: User[] may still be one WHAT binding, while
/// 'params FileInfo[] from' represents repeated FROM values.
/// </summary>
public sealed record TypeShape(
    Type ValueType,
    Type? ElementType,
    CollectionShapeKind Kind)
{
    public bool IsCollection => Kind != CollectionShapeKind.Scalar;

    public static TypeShape Analyze(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (type.IsArray)
            return new(type, type.GetElementType(), CollectionShapeKind.Array);

        if (type != typeof(string))
        {
            Type? enumerable = (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                ? type
                : type.GetInterfaces().FirstOrDefault(x =>
                    x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            if (enumerable != null)
                return new(type, enumerable.GetGenericArguments()[0], CollectionShapeKind.Sequence);
        }

        return new(type, null, CollectionShapeKind.Scalar);
    }
}
