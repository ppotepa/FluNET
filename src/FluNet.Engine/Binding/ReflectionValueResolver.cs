using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace FluNET.Binding;

/// <summary>
/// Convention fallback for CLR types. Resolution order is enum, TryParse, Parse,
/// TypeConverter and finally a public T(string) constructor.
/// </summary>
public sealed class ReflectionValueResolver : IValueResolver
{
    public bool CanResolve(Type targetType) => targetType != typeof(string);

    public object? Resolve(string value, Type targetType) =>
        Resolve(value, targetType, new ResolutionContext(targetType));

    public object? Resolve(string value, Type targetType, ResolutionContext context)
    {
        Type actualType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (actualType.IsEnum)
            return Enum.Parse(actualType, value, ignoreCase: true);

        MethodInfo? tryParse = actualType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(method =>
            {
                ParameterInfo[] parameters = method.GetParameters();
                return method.Name == "TryParse"
                    && method.ReturnType == typeof(bool)
                    && parameters.Length == 2
                    && parameters[0].ParameterType == typeof(string)
                    && parameters[1].IsOut
                    && parameters[1].ParameterType.GetElementType() == actualType;
            });

        if (tryParse != null)
        {
            object?[] args = [value, null];
            if (tryParse.Invoke(null, args) is true)
                return args[1];
        }

        MethodInfo? parse = actualType.GetMethod(
            "Parse",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(string)],
            modifiers: null);
        if (parse != null && actualType.IsAssignableFrom(parse.ReturnType))
            return parse.Invoke(null, [value]);

        TypeConverter converter = TypeDescriptor.GetConverter(actualType);
        if (converter.CanConvertFrom(typeof(string)))
            return converter.ConvertFrom(null, context.EffectiveCulture, value);

        ConstructorInfo? stringConstructor = actualType.GetConstructor([typeof(string)]);
        if (stringConstructor != null)
            return stringConstructor.Invoke([value]);

        return null;
    }
}
