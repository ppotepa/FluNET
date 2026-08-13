using FluNET.Language;

namespace FluNET.Variables;

public sealed partial class VariableStore : IVariableStore
{
    private readonly LanguageSnapshot _language;

    public VariableStore(LanguageSnapshot language)
    {
        _language = language ?? throw new ArgumentNullException(nameof(language));
    }
}
