namespace FluNET.Language.Values;

/// <summary>Canonical conversion used when a line/list producer feeds a Text consumer.</summary>
public sealed class TextListToTextConversion : IValueConversion<IReadOnlyList<string>, string>
{
    public string Convert(
        IReadOnlyList<string> value,
        ValueConversionContext context) =>
        string.Join(
            " ",
            value.Where(line => !string.IsNullOrEmpty(line))
                .Select(line => line.TrimEnd('.')));
}
