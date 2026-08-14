using FluNET.Language;
using FluNET.Language.Values;
using FluNET.Prompt;
using FluNET.Variables;

namespace FluNET.Compilation;

public sealed partial class TypedProgramTypeValidator
{
    private readonly LanguageSnapshot? _language;
    private readonly IVariableResolver? _variables;
    private readonly IValueCodecRegistry? _values;

    public TypedProgramTypeValidator()
    {
    }

    public TypedProgramTypeValidator(
        LanguageSnapshot language,
        IValueCodecRegistry values)
    {
        _language = language ?? throw new ArgumentNullException(nameof(language));
        _values = values ?? throw new ArgumentNullException(nameof(values));
    }

    public TypedProgramTypeValidator(
        LanguageSnapshot language,
        IVariableResolver variables,
        IValueCodecRegistry values)
        : this(language, values)
    {
        _variables = variables ?? throw new ArgumentNullException(nameof(variables));
    }

    private void ValidateType(
        string variableName,
        TypeSymbol source,
        TypeSymbol target,
        SourceSpan span)
    {
        if (source.Id == target.Id || target.IsAssignableFrom(source))
        {
            return;
        }

        if (_values is not null)
        {
            ConversionResolution conversion = _values.ResolveConversion(source, target);
            if (conversion.IsAmbiguous)
            {
                throw new CommandCompilationException(
                    "FLN152",
                    $"Variable '[{variableName}]' has ambiguous implicit conversions " +
                    $"from '{source}' to '{target}'.",
                    span);
            }
            if (conversion.Path is not null)
            {
                return;
            }
        }

        throw new CommandCompilationException(
            "FLN151",
            $"Variable '[{variableName}]' has type '{source}', but '{target}' is required.",
            span);
    }
}
