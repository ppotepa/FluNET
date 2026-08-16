using System.Globalization;

namespace FluNET.Binding;

/// <summary>
/// CLR-backed value binding used by the future binder and available to verbs during migration.
/// </summary>
public sealed class ValueResolverRegistry
{
    private readonly List<IValueResolver> _resolvers = [];

    public ValueResolverRegistry()
    {
        Add(new StringResolver());
        Add(new FileInfoResolver());
        Add(new UriResolver());
        Add(new PrimitiveResolver());
    }

    public ValueResolverRegistry Add(IValueResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        _resolvers.Insert(0, resolver);
        return this;
    }

    public bool TryResolve(string value, Type targetType, out object? resolved)
    {
        foreach (IValueResolver resolver in _resolvers)
        {
            if (!resolver.CanResolve(targetType))
                continue;

            try
            {
                resolved = resolver.Resolve(value, targetType);
                return resolved != null || !targetType.IsValueType;
            }
            catch
            {
                // Try the next compatible resolver. Binding diagnostics are produced by the binder.
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
