using FluNET.Keywords;
using FluNET.Syntax.Core;
using System.Reflection;

namespace FluNET.Language;

/// <summary>
/// Mutable registration facade used while composing a FluNET language. New verb descriptors
/// can be discovered without constructing the verb; legacy word creation still uses prototypes.
/// </summary>
public sealed class LanguageRegistry
{
    private readonly Dictionary<string, WordDescriptor> _words = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<VerbDescriptor>> _verbs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, QualifierDescriptor> _qualifiers = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Assembly> _assemblies = [];
    private readonly LanguageCompiler _compiler = new();

    public LanguageRegistry()
    {
        RegisterStandardQualifiers();
        RegisterAssemblies(AppDomain.CurrentDomain.GetAssemblies());
    }

    public IReadOnlyCollection<WordDescriptor> Words => _words.Values.DistinctBy(x => x.WordType).ToArray();
    public IReadOnlyCollection<VerbDescriptor> Verbs => _verbs.Values.SelectMany(x => x).DistinctBy(x => x.VerbType).ToArray();
    public IReadOnlyCollection<QualifierDescriptor> Qualifiers => _qualifiers.Values.ToArray();
    public LanguageSnapshot Snapshot => new(Words, Verbs, Qualifiers);

    public void RegisterAssemblies(IEnumerable<Assembly> assemblies)
    {
        foreach (Assembly assembly in assemblies)
        {
            if (!_assemblies.Add(assembly)) continue;

            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(x => x != null).Cast<Type>().ToArray(); }

            foreach (Type type in types.Where(x => typeof(IWord).IsAssignableFrom(x) && !x.IsAbstract && !x.IsInterface))
                RegisterWord(type);
        }
    }

    public void Refresh()
    {
        _words.Clear();
        _verbs.Clear();
        _assemblies.Clear();
        RegisterAssemblies(AppDomain.CurrentDomain.GetAssemblies());
    }

    public void RegisterQualifier(string text, Type? valueType = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        _qualifiers[text] = new QualifierDescriptor(text.ToUpperInvariant(), valueType);
    }

    public bool IsQualifier(string text) => _qualifiers.ContainsKey(text);

    public bool TryCreateWord(string text, out IWord? word)
    {
        if (_words.TryGetValue(text, out WordDescriptor? descriptor))
        {
            word = descriptor.Factory();
            return word != null;
        }

        word = null;
        return false;
    }

    public bool TryGetVerb(string text, out VerbDescriptor? descriptor)
    {
        IReadOnlyList<VerbDescriptor> overloads = GetVerbOverloads(text);
        descriptor = overloads.FirstOrDefault();
        return descriptor != null;
    }

    public IReadOnlyList<VerbDescriptor> GetVerbOverloads(string text) =>
        _verbs.TryGetValue(text, out List<VerbDescriptor>? overloads)
            ? overloads.DistinctBy(x => x.VerbType).ToArray()
            : [];

    public Type? GetVerbBaseType(string text)
    {
        VerbDescriptor? descriptor = GetVerbOverloads(text).FirstOrDefault();
        if (descriptor == null) return null;

        Type? baseType = descriptor.VerbType.BaseType;
        while (baseType != null && !baseType.IsAbstract && baseType != typeof(object))
            baseType = baseType.BaseType;

        if (baseType == null || baseType == typeof(object)) return null;
        return baseType.IsGenericType ? baseType.GetGenericTypeDefinition() : baseType;
    }

    private void RegisterWord(Type type)
    {
        Func<IWord?> factory = () => CreatePrototype(type) as IWord;
        IWord? prototype = factory();

        if (typeof(IVerb).IsAssignableFrom(type))
        {
            IVerb? verbPrototype = prototype as IVerb;
            VerbIdentity? identity = _compiler.DescribeVerbIdentity(type, verbPrototype);
            if (identity != null)
            {
                VerbDescriptor descriptor = _compiler.DescribeVerb(type, identity.Text, identity.Synonyms, () => factory() as IVerb);
                RegisterOverload(identity.Text, descriptor);
                foreach (string synonym in identity.Synonyms)
                    RegisterOverload(synonym, descriptor);

                if (prototype is IKeyword)
                {
                    var word = new WordDescriptor(type, identity.Text, identity.Synonyms, factory);
                    _words.TryAdd(identity.Text, word);
                    foreach (string synonym in identity.Synonyms)
                        _words.TryAdd(synonym, word);
                }
            }

            return;
        }

        if (prototype is not IKeyword keyword)
            return;

        var nonVerbWord = new WordDescriptor(type, keyword.Text, [], factory);
        _words.TryAdd(keyword.Text, nonVerbWord);
    }

    private void RegisterOverload(string keyword, VerbDescriptor descriptor)
    {
        if (!_verbs.TryGetValue(keyword, out List<VerbDescriptor>? overloads))
            _verbs[keyword] = overloads = [];
        if (overloads.All(x => x.VerbType != descriptor.VerbType))
            overloads.Add(descriptor);
    }

    private static object? CreatePrototype(Type type)
    {
        try
        {
            ConstructorInfo? parameterless = type.GetConstructor(Type.EmptyTypes);
            if (parameterless != null)
                return parameterless.Invoke(null);

            ConstructorInfo? constructor = type.GetConstructors().OrderBy(x => x.GetParameters().Length).FirstOrDefault();
            if (constructor == null) return null;

            object?[] arguments = constructor.GetParameters()
                .Select(p => p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null)
                .ToArray();
            return constructor.Invoke(arguments);
        }
        catch { return null; }
    }

    private void RegisterStandardQualifiers()
    {
        RegisterQualifier("TEXT", typeof(string));
        RegisterQualifier("JSON");
        RegisterQualifier("XML");
        RegisterQualifier("BINARY", typeof(byte[]));
        foreach (string qualifier in new[] { "CSV", "HTML", "YAML", "IMAGE", "VIDEO", "AUDIO" })
            RegisterQualifier(qualifier);
    }
}
