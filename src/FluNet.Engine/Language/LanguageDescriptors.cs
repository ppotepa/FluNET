using FluNET.Syntax.Core;
using FluNET.Language.Metadata;

namespace FluNET.Language;

public sealed record VerbIdentity(string Text, IReadOnlyList<string> Synonyms);

public sealed record WordDescriptor(
    Type WordType,
    string Text,
    IReadOnlyList<string> Synonyms,
    Func<IWord?> Factory) : ILanguageElement
{
    public string StableId => $"word:{Text.ToLowerInvariant()}:{WordType.FullName}";
    public string Name => Text;
}

public sealed record VerbDescriptor(
    Type VerbType,
    string Text,
    IReadOnlyList<string> Synonyms,
    SentencePattern Pattern,
    Func<IVerb?> Factory) : ILanguageElement
{
    public string StableId => $"verb:{Text.ToLowerInvariant()}:{VerbType.FullName}";
    public string Name => Text;
    public IReadOnlyList<ConstructorDescriptor> Constructors { get; init; } = [];
    public Type? ResultType { get; init; }
    public Type? FamilyType { get; init; }
    public IReadOnlyList<string> Capabilities { get; init; } = [];
}

public sealed record QualifierDescriptor(string Text, Type? ValueType = null) : ILanguageElement
{
    public string StableId => $"qualifier:{Text.ToLowerInvariant()}";
    public string Name => Text;
}
