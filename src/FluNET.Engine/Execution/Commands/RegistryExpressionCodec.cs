using FluNET.Language;
using FluNET.Language.Values;

namespace FluNET.Execution.Commands;

internal sealed class RegistryExpressionCodec<TValue>(
    LanguageSnapshot language,
    IValueCodecRegistry values) : FluNET.Execution.Commands.IValueCodec<TValue>
{
    public TValue Decode(object value)
    {
        if (value is TValue typed)
        {
            return typed;
        }

        TypeSymbol source = language.Types.Get(value.GetType());
        TypeSymbol target = language.Types.Get<TValue>();
        ConversionResolution resolution = values.ResolveConversion(source, target);
        if (resolution.IsAmbiguous)
        {
            throw new InvalidCastException(
                $"Conversion from '{source}' to '{target}' is ambiguous.");
        }
        if (resolution.Path is null)
        {
            throw new InvalidCastException(
                $"No implicit conversion exists from '{source}' to '{target}'.");
        }

        object converted = values.Convert(value, resolution.Path);
        if (converted is TValue result)
        {
            return result;
        }
        if (target.Id == BuiltInTypeIds.Number)
        {
            return NumericRuntimeAdapter.ConvertTo<TValue>(converted);
        }

        throw new InvalidCastException(
            $"Conversion from '{source}' to '{target}' returned '{converted.GetType()}'.");
    }
}
