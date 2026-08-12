using FluNET.Keywords;
using FluNET.Language;
using FluNET.Syntax.Core;

namespace FluNET.Syntax.Registry;

/// <summary>
/// Compatibility registry projected only from frames that still expose an
/// IVerb adapter. Native typed-command frames remain exclusively in the
/// canonical LanguageSnapshot and are never materialized as legacy words.
/// </summary>
public sealed class LanguageRegistry
{
    private readonly LanguageSnapshot _snapshot;
    private IReadOnlyList<Type> _words = Array.Empty<Type>();
    private IReadOnlyList<Type> _verbs = Array.Empty<Type>();
    private IReadOnlyList<Type> _nouns = Array.Empty<Type>();
    private Dictionary<string, Type> _wordTypes = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, Type> _verbTypes = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, Type> _verbBaseTypes = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<Type, Type> _concreteToBase = [];

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
            .Distinct()
            .OrderBy(type => type.AssemblyQualifiedName, StringComparer.Ordinal)
            .ToArray();

        Dictionary<string, Type> wordTypes = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, Type> verbTypes = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, Type> verbBaseTypes = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<Type, Type> concreteToBase = [];

        foreach (CommandDescriptor command in _snapshot.Commands)
        {
            CommandFrameDescriptor[] commandLegacyFrames = command.Frames
                .Where(frame => frame.HasLegacyVerbAdapter)
                .ToArray();
            if (commandLegacyFrames.Length == 0)
            {
                continue;
            }

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
        }

        _words = words;
        _verbs = verbs;
        _nouns = words.Where(type => typeof(INoun).IsAssignableFrom(type)).ToArray();
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

    private static T CreatePrototype<T>(Type type) where T : class
    {
        if (type.GetConstructor(Type.EmptyTypes) is null)
        {
            throw new LanguageRegistrationException(
                $"Language type '{type.FullName}' must provide a public parameterless constructor " +
                "while the legacy word-chain adapter is enabled.");
        }

        return Activator.CreateInstance(type) as T
            ?? throw new LanguageRegistrationException(
                $"Could not create language type '{type.FullName}'.");
    }
}

public sealed class LanguageRegistrationException : Exception
{
    public LanguageRegistrationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
