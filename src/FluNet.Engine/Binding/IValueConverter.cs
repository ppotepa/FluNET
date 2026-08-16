using System.Globalization;

namespace FluNET.Binding;

public sealed record ValueConversion(
    Type SourceType,
    Type TargetType,
    int Cost,
    Func<object?, object?> Apply);

public interface IValueConverter
{
    Type SourceType { get; }
    Type TargetType { get; }
    int Cost { get; }
    object? Convert(object? value);
}

public abstract class ValueConverter<TFrom, TTo>(int cost = 2) : IValueConverter
{
    public Type SourceType => typeof(TFrom);
    public Type TargetType => typeof(TTo);
    public int Cost { get; } = cost;
    public object? Convert(object? value) => value is null ? default(TTo) : Convert((TFrom)value);
    protected abstract TTo Convert(TFrom value);
}

/// <summary>
/// Runtime conversion graph used after a value already has a CLR type. This is separate
/// from textual IValueResolver resolution.
/// </summary>
public sealed class ValueConversionRegistry
{
    private static readonly HashSet<Type> NumericTypes =
    [typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal)];

    private readonly List<IValueConverter> _converters = [];

    public ValueConversionRegistry Add(IValueConverter converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        _converters.Insert(0, converter);
        return this;
    }

    public bool TryGet(Type sourceType, Type targetType, out ValueConversion? conversion)
    {
        if (sourceType == targetType)
        {
            conversion = new(sourceType, targetType, 0, value => value);
            return true;
        }

        if (targetType.IsAssignableFrom(sourceType))
        {
            conversion = new(sourceType, targetType, 1, value => value);
            return true;
        }

        Type sourceActual = Nullable.GetUnderlyingType(sourceType) ?? sourceType;
        Type targetActual = Nullable.GetUnderlyingType(targetType) ?? targetType;

        IValueConverter? custom = _converters
            .Where(x => x.SourceType.IsAssignableFrom(sourceActual) && targetActual.IsAssignableFrom(x.TargetType))
            .OrderBy(x => x.Cost)
            .FirstOrDefault();
        if (custom != null)
        {
            conversion = new(sourceType, targetType, custom.Cost, custom.Convert);
            return true;
        }

        if (NumericTypes.Contains(sourceActual) && NumericTypes.Contains(targetActual))
        {
            conversion = new(sourceType, targetType, 2, value => value is null ? null : System.Convert.ChangeType(value, targetActual, CultureInfo.InvariantCulture));
            return true;
        }

        conversion = null;
        return false;
    }
}
