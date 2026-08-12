using FluNET.Keywords;
using FluNET.Syntax.Core;
using System.Reflection;

namespace FluNET.Syntax.Registry;

/// <summary>
/// Single deterministic registry for words, verbs, synonyms, and verb families.
/// Discovery happens in one place so tokenization, validation, and execution use
/// the same language definition.
/// </summary>
public sealed class LanguageRegistry
{
    private IReadOnlyList<Type> _words = Array.Empty<Type>();
    private IReadOnlyList<Type> _verbs = Array.Empty<Type>();
    private IReadOnlyList<Type> _nouns = Array.Empty<Type>();
    private Dictionary<string, Type> _wordTypes = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, Type> _verbTypes = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, Type> _verbBaseTypes = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<Type, Type> _concreteToBase = [];

    public LanguageRegistry()
    {
        Refresh();
    }

    public IReadOnlyList<Type> Words => _words;

    public IReadOnlyList<Type> Verbs => _verbs;

    public IReadOnlyList<Type> Nouns => _nouns;

    public IEnumerable<string> VerbNames => _verbTypes.Keys.Order(StringComparer.OrdinalIgnoreCase);

    public void Refresh()
    {
        Type[] words = AppDomain.CurrentDomain.GetAssemblies()
            .OrderBy(assembly => assembly.FullName, StringComparer.Ordinal)
            .SelectMany(GetLoadableTypes)
            .Where(type => typeof(IWord).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
            .Distinct()
            .OrderBy(type => type.AssemblyQualifiedName, StringComparer.Ordinal)
            .ToArray();

        Type[] verbs = words.Where(type => typeof(IVerb).IsAssignableFrom(type)).ToArray();
        Type[] nouns = words.Where(type => typeof(INoun).IsAssignableFrom(type)).ToArray();
        Dictionary<string, Type> wordTypes = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, Type> verbTypes = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, Type> verbBaseTypes = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<Type, Type> concreteToBase = [];

        foreach (Type verbType in verbs)
        {
            IVerb verb = CreatePrototype<IVerb>(verbType);
            Type baseType = GetVerbBaseType(verbType);
            concreteToBase.Add(verbType, baseType);

            RegisterVerbName(verb.Text, verbType, baseType, verbTypes, verbBaseTypes);
            foreach (string synonym in verb.Synonyms)
            {
                RegisterVerbName(synonym, verbType, baseType, verbTypes, verbBaseTypes);
            }
        }

        foreach (Type wordType in words.Where(type => !typeof(IVerb).IsAssignableFrom(type)))
        {
            if (wordType.GetConstructor(Type.EmptyTypes) is null)
            {
                continue;
            }

            if (Activator.CreateInstance(wordType) is not IKeyword keyword)
            {
                continue;
            }

            if (!wordTypes.TryAdd(keyword.Text, wordType))
            {
                Type existing = wordTypes[keyword.Text];
                throw new LanguageRegistrationException(
                    $"Keyword '{keyword.Text}' is declared by both '{existing.FullName}' and '{wordType.FullName}'.");
            }
        }

        foreach ((string name, Type type) in verbTypes)
        {
            if (wordTypes.ContainsKey(name))
            {
                throw new LanguageRegistrationException(
                    $"'{name}' is registered as both a verb and a non-verb keyword.");
            }
            wordTypes.Add(name, type);
        }

        _words = words;
        _verbs = verbs;
        _nouns = nouns;
        _wordTypes = wordTypes;
        _verbTypes = verbTypes;
        _verbBaseTypes = verbBaseTypes;
        _concreteToBase = concreteToBase;
    }

    public IWord? CreateWord(string text)
    {
        return _wordTypes.TryGetValue(text, out Type? type)
            ? CreatePrototype<IWord>(type)
            : null;
    }

    public Type? GetVerbType(string name) =>
        _verbTypes.TryGetValue(name, out Type? type) ? type : null;

    public Type? GetVerbBaseType(string name) =>
        _verbBaseTypes.TryGetValue(name, out Type? type) ? type : null;

    public Type? GetBaseTypeForConcrete(Type concreteType) =>
        _concreteToBase.TryGetValue(concreteType, out Type? type) ? type : null;

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            string details = string.Join("; ", exception.LoaderExceptions
                .OfType<Exception>()
                .Select(loaderException => loaderException.Message));
            throw new LanguageRegistrationException(
                $"Could not inspect assembly '{assembly.FullName}': {details}",
                exception);
        }
    }

    private static T CreatePrototype<T>(Type type) where T : class
    {
        if (type.GetConstructor(Type.EmptyTypes) is null)
        {
            throw new LanguageRegistrationException(
                $"Language type '{type.FullName}' must provide a public parameterless constructor for discovery.");
        }

        return Activator.CreateInstance(type) as T
            ?? throw new LanguageRegistrationException(
                $"Could not create language type '{type.FullName}'.");
    }

    private static Type GetVerbBaseType(Type verbType)
    {
        Type? baseType = verbType.BaseType;
        while (baseType is not null && !baseType.IsAbstract && baseType != typeof(object))
        {
            baseType = baseType.BaseType;
        }

        if (baseType is null || baseType == typeof(object))
        {
            throw new LanguageRegistrationException(
                $"Verb '{verbType.FullName}' must inherit from an abstract verb family.");
        }

        return baseType.IsGenericType ? baseType.GetGenericTypeDefinition() : baseType;
    }

    private static void RegisterVerbName(
        string name,
        Type verbType,
        Type baseType,
        IDictionary<string, Type> verbTypes,
        IDictionary<string, Type> verbBaseTypes)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new LanguageRegistrationException($"Verb '{verbType.FullName}' declares an empty name.");
        }

        if (verbBaseTypes.TryGetValue(name, out Type? existingBase) && existingBase != baseType)
        {
            throw new LanguageRegistrationException(
                $"Verb name or synonym '{name}' maps to both '{existingBase.FullName}' and '{baseType.FullName}'.");
        }

        verbBaseTypes[name] = baseType;
        verbTypes.TryAdd(name, verbType);
    }
}

public sealed class LanguageRegistrationException : Exception
{
    public LanguageRegistrationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
