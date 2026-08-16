namespace FluNET.Language.Values;

/// <summary>
/// Creates the built-in value registry for standalone expression binders and
/// host integrations when no module runtime service provider is available.
/// </summary>
public static class ValueCodecRegistryFactory
{
    public static IValueCodecRegistry CreateDefault(LanguageSnapshot language)
    {
        ArgumentNullException.ThrowIfNull(language);
        ValueConversionRegistration listToText = new(
            typeof(IReadOnlyList<string>),
            typeof(string),
            typeof(TextListToTextConversion),
            ConversionKind.Implicit,
            1,
            (_, descriptor) => new RuntimeValueConversion<IReadOnlyList<string>, string>(
                descriptor,
                new TextListToTextConversion()));

        return new ValueCodecRegistry(
            language,
            EmptyServiceProvider.Instance,
            Array.Empty<ValueCodecRegistration>(),
            new[] { listToText });
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static EmptyServiceProvider Instance { get; } = new();
        public object? GetService(Type serviceType) => null;
    }
}
