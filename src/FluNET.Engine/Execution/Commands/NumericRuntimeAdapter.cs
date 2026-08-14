using System.Globalization;

namespace FluNET.Execution.Commands;

internal static class NumericRuntimeAdapter
{
    public static TValue ConvertTo<TValue>(object value)
    {
        object converted = ConvertTo(value, typeof(TValue));
        return converted is TValue typed
            ? typed
            : throw new InvalidCastException(
                $"Number runtime adapter returned '{converted.GetType()}', expected '{typeof(TValue)}'.");
    }

    public static object ConvertTo(object value, Type targetType)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(targetType);
        decimal number = ToDecimal(value);
        Type target = Nullable.GetUnderlyingType(targetType) ?? targetType;

        checked
        {
            if (target == typeof(decimal)) return number;
            if (target == typeof(byte)) return (byte)number;
            if (target == typeof(sbyte)) return (sbyte)number;
            if (target == typeof(short)) return (short)number;
            if (target == typeof(ushort)) return (ushort)number;
            if (target == typeof(int)) return (int)number;
            if (target == typeof(uint)) return (uint)number;
            if (target == typeof(long)) return (long)number;
            if (target == typeof(ulong)) return (ulong)number;
            if (target == typeof(float)) return (float)number;
            if (target == typeof(double)) return (double)number;
        }

        throw new InvalidCastException(
            $"CLR type '{targetType}' is not a supported Number runtime representation.");
    }

    public static decimal ToDecimal(object value) => value switch
    {
        decimal number => number,
        byte number => number,
        sbyte number => number,
        short number => number,
        ushort number => number,
        int number => number,
        uint number => number,
        long number => number,
        ulong number => number,
        float number when float.IsFinite(number) => (decimal)number,
        double number when double.IsFinite(number) => (decimal)number,
        string text when decimal.TryParse(
            text,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out decimal parsed) => parsed,
        _ => throw new InvalidCastException(
            $"Value '{value}' cannot be represented as FluNET Number.")
    };
}
