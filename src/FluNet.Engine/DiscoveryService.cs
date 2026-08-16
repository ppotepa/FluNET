using FluNET.Language;
using FluNET.Syntax.Core;
using System.Diagnostics.CodeAnalysis;

namespace FluNET;

/// <summary>
/// Backward-compatible facade over the Classic language registry.
/// New code should consume <see cref="LanguageRegistry"/> directly.
/// </summary>
public class DiscoveryService
{
    private readonly LanguageRegistry _registry;

    public DiscoveryService()
        : this(new LanguageRegistry())
    {
    }

    public DiscoveryService(LanguageRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public LanguageRegistry Registry => _registry;

    public IReadOnlyList<Type> Words => _registry.Words.Select(x => x.WordType).ToArray();

    public IReadOnlyList<Type> Verbs => _registry.Verbs.Select(x => x.VerbType).ToArray();

    public IReadOnlyList<Type> Nouns => Words
        .Where(x => typeof(INoun).IsAssignableFrom(x))
        .ToArray();

    public Type? GetVerbBaseTypeByWord(IWord word)
    {
        if (word is not Keywords.IKeyword keyword)
            return null;

        return _registry.GetVerbBaseType(keyword.Text);
    }

    public Type? GetBaseTypeForConcrete(Type concreteType)
    {
        Type? baseType = concreteType.BaseType;
        while (baseType != null && !baseType.IsAbstract && baseType != typeof(object))
            baseType = baseType.BaseType;

        if (baseType == null || baseType == typeof(object))
            return null;

        return baseType.IsGenericType ? baseType.GetGenericTypeDefinition() : baseType;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Classic language discovery intentionally scans loaded module assemblies.")]
    public void ClearCache() => _registry.Refresh();

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Classic language discovery intentionally scans loaded module assemblies.")]
    public void RefreshAssemblies() => _registry.Refresh();
}
