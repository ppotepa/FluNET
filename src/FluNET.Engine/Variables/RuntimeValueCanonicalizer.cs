using FluNET.Language;

namespace FluNET.Variables;

/// <summary>
/// Normalizes CLR representations that share one language TypeId into the
/// canonical runtime form consumed by codecs and conversions.
/// </summary>
internal static class RuntimeValueCanonicalizer
{
    public static object Normalize(TypeSymbol type, object value)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(value);
        if (type.Id != BuiltInTypeIds.Number || value is decimal)
        {
            return value;
        }

        try
        {
            return value switch
            {
                byte number => (decimal)number,
                sbyte number => (decimal)number,
                short number => (decimal)number,
                ushort number => (decimal)number,
                int number => (decimal)number,
                uint number => (decimal)number,
                long number => (decimal)number,
                ulong number => (decimal)number,
                float number when float.IsFinite(number) => (decimal)number,
                double number when double.IsFinite(number) => (decimal)number,
                _ => throw new InvalidCastException(
                    $"CLR value '{value.GetType()}' is not a finite Number representation.")
            };
        }
        catch (OverflowException exception)
        {
            throw new InvalidCastException(
                $"CLR Number value '{value}' cannot be represented by FluNET decimal runtime form.",
                exception);
        }
    }
}
