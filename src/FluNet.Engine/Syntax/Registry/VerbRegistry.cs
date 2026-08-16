using FluNET.Language;
using FluNET.Syntax.Core;
using FluNET.Syntax.Nouns;
using System.Reflection;

namespace FluNET.Syntax.Registry;

/// <summary>
/// Runtime verb factory backed by LanguageRegistry. Reflection discovery now has one owner.
/// </summary>
public class VerbRegistry
{
    private readonly Dictionary<string, Func<IVerb>> _verbFactories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Type, VerbMetadata> _verbMetadata = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly LanguageRegistry _language;

    private record VerbMetadata(
        Type VerbType,
        ConstructorInfo ParameterizedConstructor,
        bool HasWhat,
        bool HasFrom,
        bool HasTo,
        bool HasUsing);

    public VerbRegistry(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _language = serviceProvider.GetService(typeof(LanguageRegistry)) as LanguageRegistry ?? new LanguageRegistry();
        RegisterDiscoveredVerbs();
    }

    private void RegisterDiscoveredVerbs()
    {
        foreach (VerbDescriptor descriptor in _language.Verbs)
            RegisterVerbType(descriptor);
    }

    private void RegisterVerbType(VerbDescriptor descriptor)
    {
        Type verbType = descriptor.VerbType;
        Func<IVerb> factory = () =>
            (_serviceProvider.GetService(verbType) as IVerb) ??
            descriptor.Factory() ??
            throw new InvalidOperationException($"Unable to create verb '{descriptor.Text}' ({verbType.FullName}).");

        _verbFactories[descriptor.Text] = factory;
        foreach (string synonym in descriptor.Synonyms)
            _verbFactories[synonym] = factory;

        StoreVerbMetadata(verbType);
    }

    private void StoreVerbMetadata(Type verbType)
    {
        ConstructorInfo? constructor = verbType.GetConstructors()
            .Where(c => c.GetParameters().Length > 0)
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault();

        if (constructor == null)
            return;

        Type[] interfaces = verbType.GetInterfaces();
        bool hasWhat = interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWhat<>));
        bool hasFrom = interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IFrom<>));
        bool hasTo = interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITo<>));
        bool hasUsing = interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IUsing<>));

        _verbMetadata[verbType] = new VerbMetadata(verbType, constructor, hasWhat, hasFrom, hasTo, hasUsing);
    }

    public object? CreateVerbInstance(Type verbType, params object?[] parameters)
    {
        if (!_verbMetadata.TryGetValue(verbType, out VerbMetadata? metadata))
            return Activator.CreateInstance(verbType);

        try
        {
            return metadata.ParameterizedConstructor.Invoke(parameters);
        }
        catch
        {
            return null;
        }
    }

    public (bool HasWhat, bool HasFrom, bool HasTo, bool HasUsing) GetVerbParameterInfo(Type verbType)
    {
        if (_verbMetadata.TryGetValue(verbType, out VerbMetadata? metadata))
            return (metadata.HasWhat, metadata.HasFrom, metadata.HasTo, metadata.HasUsing);

        return (false, false, false, false);
    }

    public IVerb GetVerbByName(string name)
    {
        if (_verbFactories.TryGetValue(name, out Func<IVerb>? factory))
            return factory();

        throw new VerbNotFoundException($"No verb found with name '{name}'");
    }

    public IEnumerable<string> GetAllVerbNames() => _verbFactories.Keys;
    public int Count => _verbFactories.Count;
}

public class VerbNotFoundException : Exception
{
    public VerbNotFoundException(string message) : base(message) { }
}
