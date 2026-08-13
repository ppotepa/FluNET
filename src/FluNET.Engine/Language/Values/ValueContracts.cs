namespace FluNET.Language.Values;

public readonly record struct ValueLiteral(string Text, object? Value = null)
{
    public object RawValue => Value ?? Text;
}

public sealed record ValueParseContext(LanguageSnapshot Language);
public sealed record ValueFormatContext(LanguageSnapshot Language);
public sealed record ValueConversionContext(LanguageSnapshot Language);

public interface IValueParser<out TValue>
{
    TValue Parse(ValueLiteral literal, ValueParseContext context);
}

public interface IValueFormatter<in TValue>
{
    string Format(TValue value, ValueFormatContext context);
}

public interface IValueCodec<TValue> : IValueParser<TValue>, IValueFormatter<TValue>
{
}

public interface IValueConversion<in TSource, out TTarget>
{
    TTarget Convert(TSource value, ValueConversionContext context);
}

public enum ConversionKind
{
    Implicit,
    Explicit
}
