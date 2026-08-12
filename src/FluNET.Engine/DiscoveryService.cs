using FluNET.Keywords;
using FluNET.Syntax.Core;
using FluNET.Syntax.Registry;

namespace FluNET;

/// <summary>
/// Backward-compatible facade over the single <see cref="LanguageRegistry"/>.
/// </summary>
public sealed class DiscoveryService
{
    private readonly LanguageRegistry _registry;

    public DiscoveryService() : this(new LanguageRegistry())
    {
    }

    public DiscoveryService(LanguageRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public IReadOnlyList<Type> Words => _registry.Words;

    public IReadOnlyList<Type> Verbs => _registry.Verbs;

    public IReadOnlyList<Type> Nouns => _registry.Nouns;

    internal LanguageRegistry Registry => _registry;

    public Type? GetVerbBaseTypeByWord(IWord word) =>
        word is IKeyword keyword ? _registry.GetVerbBaseType(keyword.Text) : null;

    public Type? GetBaseTypeForConcrete(Type concreteType) =>
        _registry.GetBaseTypeForConcrete(concreteType);

    public void ClearCache() => _registry.Refresh();

    public void RefreshAssemblies() => _registry.Refresh();
}
