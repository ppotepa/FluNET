namespace FluNET.Binding;

/// <summary>
/// Converts textual surface values into CLR values. Context-aware overloads preserve
/// compatibility with simple resolvers while allowing role/verb-sensitive resolution.
/// </summary>
public interface IValueResolver
{
    bool CanResolve(Type targetType);
    object? Resolve(string value, Type targetType);

    bool CanResolve(Type targetType, ResolutionContext context) => CanResolve(targetType);
    object? Resolve(string value, Type targetType, ResolutionContext context) => Resolve(value, targetType);
}

public abstract class ValueResolver<T> : IValueResolver
{
    public bool CanResolve(Type targetType) => targetType == typeof(T);

    public object? Resolve(string value, Type targetType) => Resolve(value);

    protected abstract T? Resolve(string value);
}
