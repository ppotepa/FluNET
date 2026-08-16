using System.Collections;
using System.Globalization;
using System.Reflection;

namespace FluNET.Binding;

/// <summary>
/// Ordered CLR value-resolution pipeline. Explicit resolvers run first; reflection
/// conventions are the final fallback.
/// </summary>
public sealed class ValueResolverRegistry
{
    private readonly List<IValueResolver> _resolvers = [];
    private readonly IValueResolver _reflectionFallback = new ReflectionValueResolver();

    public ValueResolverRegistry()
    {
        _resolvers.Add(new StringResolver());
        _resolvers.Add(new FileInfoResolver());
        _resolvers.Add(new UriResolver());
        _resolvers.Add(new PrimitiveResolver());
    }

    public ValueResolverRegistry Add(IValueResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        _resolvers.Insert(0, resolver);
        return this;
    }

    public bool TryResolve(string value, Type targetType, out object? resolved) =>
        TryResolve(value, targetType, new ResolutionContext(targetType), out resolved);

    public bool TryResolve(string value, Type targetType, ResolutionContext context, out object? resolved)
    {
        foreach (IValueResolver resolver in _resolvers.Append(_reflectionFallback))
        {
            if (!resolver.CanResolve(targetType, context))
                continue;

            try
            {
                resolved = resolver.Resolve(value, targetType, context);
                if (resolved != null || !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null)
                    return true;
            }
            catch
            {
                // Continue through the ordered resolver chain. Binder diagnostics explain failure.
            }
        }

        resolved = null;
        return false;
    }

    public bool TryResolve<T>(string value, out T? resolved)
    {
        if (TryResolve(value, typeof(T), out object? result) && result is T typed)
        {
            resolved = typed;
            return true;
        }

        resolved = default;
        return false;
    }

    /// <summary>
    /// Resolves repeated syntactic values into an array/list/sequence CLR shape.
    /// </summary>
    public bool TryResolveMany(IEnumerable<string> values, Type targetType, ResolutionContext context, out object? resolved)
    {
        Type? elementType = targetType.IsArray
            ? targetType.GetElementType()
            : targetType.GetInterfaces()
                .Concat(targetType.IsInterface ? [targetType] : [])
                .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                ?.GetGenericArguments()[0];

        if (elementType == null)
        {
            resolved = null;
            return false;
        }

        var items = new List<object?>();
        foreach (string value in values)
        {
            ResolutionContext elementContext = context with { ExpectedType = elementType };
            if (!TryResolve(value, elementType, elementContext, out object? item))
            {
                resolved = null;
                return false;
            }
            items.Add(item);
        }

        if (targetType.IsArray)
        {
            Array array = Array.CreateInstance(elementType, items.Count);
            for (int i = 0; i < items.Count; i++) array.SetValue(items[i], i);
            resolved = array;
            return true;
        }

        Type listType = typeof(List<>).MakeGenericType(elementType);
        IList list = (IList)Activator.CreateInstance(listType)!;
        foreach (object? item in items) list.Add(item);

        if (targetType.IsAssignableFrom(listType) || targetType.IsInterface)
        {
            resolved = list;
            return true;
        }

        ConstructorInfo? sequenceConstructor = targetType.GetConstructor([typeof(IEnumerable<>).MakeGenericType(elementType)]);
        if (sequenceConstructor != null)
        {
            resolved = sequenceConstructor.Invoke([list]);
            return true;
        }

        resolved = null;
        return false;
    }

    private sealed class StringResolver : ValueResolver<string>
    {
        protected override string Resolve(string value) => value;
    }

    private sealed class FileInfoResolver : ValueResolver<FileInfo>
    {
        protected override FileInfo Resolve(string value) => new(value.Trim('{', '}', ' '));
    }

    private sealed class UriResolver : ValueResolver<Uri>
    {
        protected override Uri? Resolve(string value) =>
            Uri.TryCreate(value.Trim('{', '}', ' '), UriKind.RelativeOrAbsolute, out Uri? uri) ? uri : null;
    }

    private sealed class PrimitiveResolver : IValueResolver
    {
        private static readonly HashSet<Type> Supported =
        [typeof(bool), typeof(byte), typeof(short), typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal), typeof(DateTime), typeof(Guid)];

        public bool CanResolve(Type targetType) => Supported.Contains(Nullable.GetUnderlyingType(targetType) ?? targetType);

        public object? Resolve(string value, Type targetType)
        {
            Type actual = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (actual == typeof(Guid)) return Guid.Parse(value);
            if (actual == typeof(DateTime)) return DateTime.Parse(value, CultureInfo.InvariantCulture);
            return Convert.ChangeType(value, actual, CultureInfo.InvariantCulture);
        }
    }
}
