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

        if (typeof(TValue) == typeof(string) && value is IEnumerable<string> lines)
        {
            string text = string.Join(
                " ",
                lines.Where(line => !string.IsNullOrEmpty(line))
                    .Select(line => line.TrimEnd('.')));
            return (TValue)(object)text;
        }

        TypeSymbol source;
        try
        {
            source = language.Types.Get(value.GetType());
        }
        catch (LanguageDefinitionException)
        {
            if (typeof(TValue) == typeof(string))
            {
                return (TValue)(object)(value.ToString() ?? string.Empty);
            }
            throw;
        }

        TypeSymbol target = language.Types.Get<TValue>();
        ConversionResolution resolution = ResolveStrict(source, target, values);
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
        return converted is TValue result
            ? result
            : throw new InvalidCastException(
                $"Conversion from '{source}' to '{target}' returned '{converted.GetType()}'.");
    }

    private static ConversionResolution ResolveStrict(
        TypeSymbol source,
        TypeSymbol target,
        IValueCodecRegistry values)
    {
        if (source.Id == target.Id)
        {
            return new ConversionResolution(ConversionPath.Identity, false);
        }

        if (target.Id == BuiltInTypeIds.Text)
        {
            ValueConversionDescriptor[] direct = values.FindConversions(source.Id)
                .Where(edge =>
                    edge.TargetType == target.Id &&
                    edge.Kind == ConversionKind.Implicit)
                .OrderBy(edge => edge.Cost)
                .ToArray();
            if (direct.Length > 0)
            {
                int best = direct[0].Cost;
                ValueConversionDescriptor[] matches = direct
                    .Where(edge => edge.Cost == best)
                    .ToArray();
                return matches.Length == 1
                    ? new ConversionResolution(new ConversionPath(matches, best), false)
                    : new ConversionResolution(null, true);
            }
        }

        return values.ResolveConversion(source, target);
    }
}
