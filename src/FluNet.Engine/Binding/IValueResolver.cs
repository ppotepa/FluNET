namespace FluNET.Binding;

/// <summary>
/// Converts the textual surface language into CLR values. This separates value
/// binding (file.txt -> FileInfo, URL -> Uri) from verb execution.
/// </summary>
public interface IValueResolver
{
    bool CanResolve(Type targetType);
    object? Resolve(string value, Type targetType);
}

public abstract class ValueResolver<T> : IValueResolver
{
    public bool CanResolve(Type targetType) => targetType == typeof(T);

    public object? Resolve(string value, Type targetType) => Resolve(value);

    protected abstract T? Resolve(string value);
}
