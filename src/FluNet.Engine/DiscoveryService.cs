using FluNET.Syntax;
using System.Diagnostics.CodeAnalysis;

namespace FluNET
{
    public class DiscoveryService
    {
        private static IEnumerable<Type> _allWords = Enumerable.Empty<Type>();
        private static IEnumerable<Type>? _verbs;
        private static IEnumerable<Type>? _nouns;

        /// <summary>
        /// Static constructor - initializes the cache when the class is first accessed
        /// </summary>
        [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "DiscoveryService requires reflection to discover IWord implementations at runtime. Types implementing IWord should be preserved.")]
        static DiscoveryService()
        {
            _allWords = AppDomain
                .CurrentDomain
                .GetAssemblies()
                .SelectMany(static x => x.GetTypes())
                .Where(x => typeof(IWord).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)
                .ToList(); // Materialize to avoid re-evaluation
        }

        public DiscoveryService()
        {
            // Cache is already initialized by static constructor
        }

        /// <summary>
        /// Gets all discovered word types (verbs and nouns)
        /// </summary>
        public IEnumerable<Type> Words => _allWords;

        /// <summary>
        /// Gets all discovered verb types
        /// </summary>
        public IEnumerable<Type> Verbs
        {
            get
            {
                _verbs ??= Words
                    .Where(x => typeof(IVerb).IsAssignableFrom(x))
                    .ToList();
                return _verbs;
            }
        }

        /// <summary>
        /// Gets all discovered noun types
        /// </summary>
        public IEnumerable<Type> Nouns
        {
            get
            {
                _nouns ??= Words
                    .Where(x => typeof(INoun).IsAssignableFrom(x))
                    .ToList();
                return _nouns;
            }
        }

        /// <summary>
        /// Clears the discovery cache and re-discovers all word types
        /// </summary>
        [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "DiscoveryService requires reflection to discover IWord implementations at runtime. Types implementing IWord should be preserved.")]
        public static void ClearCache()
        {
            _allWords = AppDomain
                .CurrentDomain
                .GetAssemblies()
                .SelectMany(x => x.GetTypes())
                .Where(x => typeof(IWord).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)
                .ToList();

            _verbs = null;
            _nouns = null;
        }
    }
}