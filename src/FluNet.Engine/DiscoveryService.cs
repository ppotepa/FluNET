using System.Diagnostics.CodeAnalysis;

namespace FluNET
{
    public class DiscoveryService
    {
        private List<Type> _allWords;
        private List<Type>? _verbs;
        private List<Type>? _nouns;

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
                // Find all types that implement IVerb or IVerb<,>
                _verbs ??= [.. Words
                        .Where(x => typeof(IVerb).IsAssignableFrom(x) ||
                                    x.GetInterfaces().Any(i => i.IsGenericType &&
                                                             i.GetGenericTypeDefinition() == typeof(IVerb<,>)))];
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
        /// Clears the discovery cache and re-discovers all word types
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
        }
    }
}