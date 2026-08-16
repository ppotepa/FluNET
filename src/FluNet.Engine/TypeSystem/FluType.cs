namespace FluNET.TypeSystem;

/// <summary>
/// FluNET types are projections of CLR types, not a competing runtime type system.
/// </summary>
public sealed record FluType(string Name, Type ClrType)
{
    public override string ToString() => Name;
}

public static class FluTypeSystem
{
    private static readonly Dictionary<Type, string> Names = new()
    {
        [typeof(string)] = "TEXT",
        [typeof(bool)] = "BOOLEAN",
        [typeof(byte[])] = "BINARY",
        [typeof(FileInfo)] = "FILE",
        [typeof(Uri)] = "URI",
        [typeof(DateTime)] = "DATE",
        [typeof(int)] = "NUMBER",
        [typeof(long)] = "NUMBER",
        [typeof(float)] = "NUMBER",
        [typeof(double)] = "NUMBER",
        [typeof(decimal)] = "NUMBER"
    };

    public static FluType FromClr(Type clrType) =>
        new(Names.TryGetValue(clrType, out string? name) ? name : clrType.Name.ToUpperInvariant(), clrType);

    public static FluType FromClr<T>() => FromClr(typeof(T));

    public static bool IsAssignable(FluType source, FluType target) =>
        target.ClrType.IsAssignableFrom(source.ClrType);
}
