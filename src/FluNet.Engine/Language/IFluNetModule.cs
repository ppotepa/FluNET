namespace FluNET.Language;

/// <summary>
/// Extension boundary for language packages such as FluNET.Http, FluNET.Sql or FluNET.Json.
/// Modules enrich vocabulary without modifying the core engine.
/// </summary>
public interface IFluNetModule
{
    void Configure(LanguageRegistry language);
}

public static class LanguageRegistryModuleExtensions
{
    public static LanguageRegistry AddModule<TModule>(this LanguageRegistry language)
        where TModule : IFluNetModule, new()
    {
        new TModule().Configure(language);
        language.RegisterAssemblies(new[] { typeof(TModule).Assembly });
        return language;
    }
}
