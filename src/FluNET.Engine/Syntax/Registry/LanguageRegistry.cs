using FluNET.Keywords;
<<<<<<< HEAD
using FluNET.Language;
using FluNET.Syntax.Core;
=======
using FluNET.Syntax.Core;
using System.Reflection;
>>>>>>> origin/agent/stabilize-poc-foundation

namespace FluNET.Syntax.Registry;

/// <summary>
<<<<<<< HEAD
/// Compatibility registry projected only from frames that still expose an
/// IVerb adapter. Native typed-command frames remain exclusively in the
/// canonical LanguageSnapshot and are never materialized as legacy words.
/// </summary>
public sealed class LanguageRegistry
{
    private readonly LanguageSnapshot _snapshot;
=======
/// Single deterministic registry for words, verbs, synonyms, and verb families.
/// Discovery happens in one place so tokenization, validation, and execution use
/// the same language definition.
/// </summary>
public sealed class LanguageRegistry
{
>>>>>>> origin/agent/stabilize-poc-foundation
    private IReadOnlyList<Type> _words = Array.Empty<Type>();
    private IReadOnlyList<Type> _verbs = Array.Empty<Type>();
    private IReadOnlyList<Type> _nouns = Array.Empty<Type>();
    private Dictionary<string, Type> _wordTypes = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, Type> _verbTypes = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, Type> _verbBaseTypes = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<Type, Type> _concreteToBase = [];

<<<<<<< HEAD
    public LanguageRegistry() : this(StandardLanguage.CreateSnapshot())
    {
    }

    public LanguageRegistry(LanguageSnapshot snapshot)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        Refresh();
    }

    public LanguageSnapshot Snapshot => _snapshot;
    public IReadOnlyList<Type> Words => _words;
    public IReadOnlyList<Type> Verbs => _verbs;
    public IReadOnlyList<Type> Nouns => _nouns;
    public IEnumerable<string> VerbNames => _verbTypes.Keys.Order(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Rebuilds the legacy word indexes from the immutable snapshot without
    /// assembly scanning or mutation of the canonical language definition.
    /// </summary>
    public void Refresh()
    {
        CommandFrameDescriptor[] legacyFrames = _snapshot.Commands
            .SelectMany(command => command.Frames)
            .Where(frame => frame.HasLegacyVerbAdapter)
            .ToArray();

        Type[] verbs = legacyFrames
            .Select(frame => frame.ImplementationType)
            .Distinct()
            .OrderBy(type => type.AssemblyQualifiedName, StringComparer.Ordinal)
            .ToArray();

        Type[] keywordTypes = _snapshot.Keywords
            .Select(keyword => keyword.ImplementationType)
            .Distinct()
            .ToArray();

        Type[] words = verbs.Concat(keywordTypes)
=======
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
>>>>>>> origin/agent/stabilize-poc-foundation
            .Distinct()
            .OrderBy(type => type.AssemblyQualifiedName, StringComparer.Ordinal)
            .ToArray();

<<<<<<< HEAD
=======
        Type[] verbs = words.Where(type => typeof(IVerb).IsAssignableFrom(type)).ToArray();
        Type[] nouns = words.Where(type => typeof(INoun).IsAssignableFrom(type)).ToArray();
>>>>>>> origin/agent/stabilize-poc-foundation
        Dictionary<string, Type> wordTypes = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, Type> verbTypes = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, Type> verbBaseTypes = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<Type, Type> concreteToBase = [];

<<<<<<< HEAD
        foreach (CommandDescriptor command in _snapshot.Commands)
        {
            CommandFrameDescriptor[] commandLegacyFrames = command.Frames
                .Where(frame => frame.HasLegacyVerbAdapter)
                .ToArray();
            if (commandLegacyFrames.Length == 0)
=======
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
>>>>>>> origin/agent/stabilize-poc-foundation
            {
                continue;
            }

<<<<<<< HEAD
            CommandFrameDescriptor primaryFrame = commandLegacyFrames
                .OrderBy(frame => frame.ImplementationType.AssemblyQualifiedName, StringComparer.Ordinal)
                .First();
            foreach (CommandFrameDescriptor frame in commandLegacyFrames)
            {
                if (!concreteToBase.TryAdd(frame.ImplementationType, frame.FamilyType) &&
                    concreteToBase[frame.ImplementationType] != frame.FamilyType)
                {
                    throw new LanguageRegistrationException(
                        $"Verb '{frame.ImplementationType.FullName}' maps to multiple families.");
                }
            }

            foreach (string surface in command.SurfaceForms)
            {
                verbTypes.Add(surface, primaryFrame.ImplementationType);
                verbBaseTypes.Add(surface, primaryFrame.FamilyType);
                wordTypes.Add(surface, primaryFrame.ImplementationType);
            }
        }

        foreach (KeywordDescriptor keyword in _snapshot.Keywords)
        {
            wordTypes.Add(keyword.Text, keyword.ImplementationType);
=======
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
>>>>>>> origin/agent/stabilize-poc-foundation
        }

        _words = words;
        _verbs = verbs;
<<<<<<< HEAD
        _nouns = words.Where(type => typeof(INoun).IsAssignableFrom(type)).ToArray();
=======
        _nouns = nouns;
>>>>>>> origin/agent/stabilize-poc-foundation
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

<<<<<<< HEAD
=======
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

>>>>>>> origin/agent/stabilize-poc-foundation
    private static T CreatePrototype<T>(Type type) where T : class
    {
        if (type.GetConstructor(Type.EmptyTypes) is null)
        {
            throw new LanguageRegistrationException(
<<<<<<< HEAD
                $"Language type '{type.FullName}' must provide a public parameterless constructor " +
                "while the legacy word-chain adapter is enabled.");
=======
                $"Language type '{type.FullName}' must provide a public parameterless constructor for discovery.");
>>>>>>> origin/agent/stabilize-poc-foundation
        }

        return Activator.CreateInstance(type) as T
            ?? throw new LanguageRegistrationException(
                $"Could not create language type '{type.FullName}'.");
    }
<<<<<<< HEAD
=======

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
>>>>>>> origin/agent/stabilize-poc-foundation
}

public sealed class LanguageRegistrationException : Exception
{
    public LanguageRegistrationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
