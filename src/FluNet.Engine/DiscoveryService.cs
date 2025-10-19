using FluNET.Syntax;

namespace FluNET
{
    public class DiscoveryService
    {
        private static IEnumerable<Type>? _allWords;
        private static IEnumerable<Type>? _verbs;
        private static IEnumerable<Type>? _nouns;

        public DiscoveryService()
        {
            // Initialize cache on first instantiation
            if (_allWords == null)
            {
                _allWords = AppDomain
                    .CurrentDomain
                    .GetAssemblies()
                    .SelectMany(x => x.GetTypes())
                    .Where(x => typeof(IWord).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)
                    .ToList(); // Materialize to avoid re-evaluation
            }
        }

        /// <summary>
        /// Gets all discovered word types (verbs and nouns)
        /// </summary>
        public IEnumerable<Type> Words
        {
            get
            {
                if (_allWords == null)
                {
                    throw new InvalidOperationException("DiscoveryService has not been initialized.");
                }
                return _allWords;
            }
        }

        /// <summary>
        /// Gets all discovered verb types
        /// </summary>
        public IEnumerable<Type> Verbs
        {
            get
            {
                if (_verbs == null)
                {
                    _verbs = Words
                        .Where(x => typeof(IVerb).IsAssignableFrom(x))
                        .ToList();
                }
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
                if (_nouns == null)
                {
                    _nouns = Words
                        .Where(x => typeof(INoun).IsAssignableFrom(x))
                        .ToList();
                }
                return _nouns;
            }
        }

        /// <summary>
        /// Clears the discovery cache, forcing re-discovery on next instantiation
        /// </summary>
        public static void ClearCache()
        {
            _allWords = null;
            _verbs = null;
            _nouns = null;
        }
    }
}