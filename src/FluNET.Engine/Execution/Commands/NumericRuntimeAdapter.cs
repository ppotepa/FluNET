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

        if (IsIntegerType(target) && number != decimal.Truncate(number))
        {
            throw new InvalidCastException(
                $"FluNET Number '{number}' cannot be represented by integer CLR type '{target}'.");
        }

        try
        {
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
        }
        catch (OverflowException exception)
        {
            throw new InvalidCastException(
                $"FluNET Number '{number}' is outside CLR type '{target}' range.",
                exception);
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
        float number when float.IsFinite(number) => ToDecimalChecked(number),
        double number when double.IsFinite(number) => ToDecimalChecked(number),
        string text when decimal.TryParse(
            text,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out decimal parsed) => parsed,
        _ => throw new InvalidCastException(
            $"Value '{value}' cannot be represented as FluNET Number.")
    };

    private static decimal ToDecimalChecked(double number)
    {
        try
        {
            return (decimal)number;
        }
        catch (OverflowException exception)
        {
            throw new InvalidCastException(
                $"CLR floating-point Number '{number}' is outside FluNET decimal runtime range.",
                exception);
        }
    }

    private static bool IsIntegerType(Type type) =>
        type == typeof(byte) ||
        type == typeof(sbyte) ||
        type == typeof(short) ||
        type == typeof(ushort) ||
        type == typeof(int) ||
        type == typeof(uint) ||
        type == typeof(long) ||
        type == typeof(ulong);
}
