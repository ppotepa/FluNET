using FluNET.Language;
using System.Globalization;

namespace FluNET.Binding;

public sealed record ResolutionContext(
    Type ExpectedType,
    ClauseKind? Role = null,
    VerbDescriptor? Verb = null,
    QualifierDescriptor? Qualifier = null,
    IServiceProvider? Services = null,
    CultureInfo? Culture = null,
    IReadOnlyDictionary<string, object?>? Variables = null)
{
    public CultureInfo EffectiveCulture => Culture ?? CultureInfo.InvariantCulture;
}
