using FluNET.Keywords;
using FluNET.Syntax.Core;
using System.Diagnostics.CodeAnalysis;

namespace FluNET
{
    public class DiscoveryService
    {
        private List<Type> _allWords;
        private List<Type>? _verbs;
        private List<Type>? _nouns;

        // Dictionary mapping verb names (including synonyms) to their base abstract classes
        private Dictionary<string, Type>? _verbBaseTypes;

        // Dictionary mapping concrete verb types to their base abstract classes
        private Dictionary<Type, Type>? _concreteToBaseMapping;

        public DiscoveryService()
        {
            _allWords = InitializeWords();
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "DiscoveryService requires reflection to discover IWord implementations at runtime. Types implementing IWord should be preserved.")]
        private static List<Type> InitializeWords()
        {
            return [.. AppDomain
                .CurrentDomain
                .GetAssemblies()
                .SelectMany(x => x.GetTypes())
                .Where(x => typeof(IWord).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)];
        }

        /// <summary>
        /// Gets all discovered word types (verbs and nouns)
        /// </summary>
        public IReadOnlyList<Type> Words => _allWords;

        /// <summary>
        /// Gets all discovered verb types
        /// </summary>
        public IReadOnlyList<Type> Verbs
        {
            get
            {
                // Find all types that implement IVerb (marker interface)
                // IVerb is the base interface that all verbs inherit from
                _verbs ??= [.. Words
                        .Where(x => typeof(IVerb).IsAssignableFrom(x))];
                return _verbs;
            }
        }

        /// <summary>
        /// Gets all discovered noun types
        /// </summary>
        public IReadOnlyList<Type> Nouns
        {
            get
            {
                _nouns ??= [.. Words
                        .Where(x => typeof(INoun).IsAssignableFrom(x))];
                return _nouns;
            }
        }

        /// <summary>
        /// Gets the base abstract class type for a verb word
        /// </summary>
        public Type? GetVerbBaseTypeByWord(IWord word)
        {
            if (word == null) return null;
            if (word is not IKeyword keyword) return null;

            InitializeVerbMappings();

            return _verbBaseTypes!.TryGetValue(keyword.Text.ToUpperInvariant(), out var baseType)
                ? baseType
                : null;
        }

        /// <summary>
        /// Gets the base abstract class type for a concrete verb type
        /// </summary>
        public Type? GetBaseTypeForConcrete(Type concreteType)
        {
            InitializeVerbMappings();

            return _concreteToBaseMapping!.TryGetValue(concreteType, out var baseType)
                ? baseType
                : null;
        }

        /// <summary>
        /// Initializes the verb mappings dictionaries if not already initialized
        /// </summary>
        private void InitializeVerbMappings()
        {
            if (_verbBaseTypes != null && _concreteToBaseMapping != null)
                return;

            _verbBaseTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            _concreteToBaseMapping = new Dictionary<Type, Type>();

            // Process each concrete verb
            foreach (var verbType in Verbs)
            {
                // Skip abstract classes
                if (verbType.IsAbstract)
                    continue;

                // Find the base abstract class in the hierarchy
                Type? baseType = verbType.BaseType;
                while (baseType != null && !baseType.IsAbstract && baseType != typeof(object))
                {
                    baseType = baseType.BaseType;
                }

                // Skip if no proper base class found
                if (baseType == null || baseType == typeof(object))
                    continue;

                // Convert to generic type definition for Lexicon compatibility
                Type baseTypeKey = baseType.IsGenericType ? baseType.GetGenericTypeDefinition() : baseType;

                // Map the concrete type to its base abstract class
                _concreteToBaseMapping[verbType] = baseTypeKey;

                // Create an instance to get the verb name and synonyms
                try
                {
                    if (Activator.CreateInstance(verbType) is IVerb verb)
                    {
                        // Register the main verb name
                        string mainVerbName = verb.Text.ToUpperInvariant();
                        _verbBaseTypes[mainVerbName] = baseTypeKey;

                        // Register all synonyms
                        if (verb.Synonyms != null)
                        {
                            foreach (string synonym in verb.Synonyms)
                            {
                                _verbBaseTypes[synonym.ToUpperInvariant()] = baseTypeKey;
                            }
                        }
                    }
                }
                catch
                {
                    // Skip verbs that can't be instantiated with parameterless constructor
                }
            }
        }

        /// <summary>
        /// Clears the discovery cache and re-discovers all word types.
        /// This forces a fresh assembly scan, useful for test isolation.
        /// </summary>
        [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "DiscoveryService requires reflection to discover IWord implementations at runtime. Types implementing IWord should be preserved.")]
        public void ClearCache()
        {
            _allWords = [.. AppDomain
                .CurrentDomain
                .GetAssemblies()
                .SelectMany(x => x.GetTypes())
                .Where(x => typeof(IWord).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)];

            _verbs = null;
            _nouns = null;
            _verbBaseTypes = null;
            _concreteToBaseMapping = null;
        }

        /// <summary>
        /// Refreshes the assembly cache by rescanning all loaded assemblies.
        /// This ensures fresh discovery results, particularly useful between test runs.
        /// </summary>
        [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "DiscoveryService requires reflection to discover IWord implementations at runtime. Types implementing IWord should be preserved.")]
        public void RefreshAssemblies()
        {
            ClearCache();
        }
    }
}