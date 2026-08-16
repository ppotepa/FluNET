namespace FluNET.Language;

/// <summary>
/// Extension boundary for language packages such as FluNET.Classic.Http or FluNET.Classic.Sql.
/// </summary>
public interface IFluNetModule
{
    string Name => GetType().Assembly.GetName().Name ?? GetType().Name;
    Version Version => GetType().Assembly.GetName().Version ?? new Version(0, 1, 0);
    IReadOnlyCollection<Type> Dependencies => Array.Empty<Type>();
    void Configure(LanguageRegistry language);
}

public static class LanguageRegistryModuleExtensions
{
    public static LanguageRegistry AddModule<TModule>(this LanguageRegistry language)
        where TModule : IFluNetModule, new()
    {
        language.RegisterModule(new TModule());
        return language;
    }
}
