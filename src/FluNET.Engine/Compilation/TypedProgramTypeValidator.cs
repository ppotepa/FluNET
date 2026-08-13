using FluNET.Language;
using FluNET.Variables;

namespace FluNET.Compilation;

public sealed partial class TypedProgramTypeValidator
{
    private readonly LanguageSnapshot? _language;
    private readonly IVariableResolver? _variables;

    public TypedProgramTypeValidator()
    {
    }

    public TypedProgramTypeValidator(
        LanguageSnapshot language,
        IVariableResolver variables)
    {
        _language = language ?? throw new ArgumentNullException(nameof(language));
        _variables = variables ?? throw new ArgumentNullException(nameof(variables));
    }
}
