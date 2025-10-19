using FluNET.Syntax;
using System.Diagnostics.CodeAnalysis;

namespace FluNET
{
    public static class DiscoveryService
    {
        private static List<Type> _allWords = InitializeWords();
        private static List<Type>? _verbs;
        private static List<Type>? _nouns;

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
        public static IReadOnlyList<Type> Words => _allWords;

        /// <summary>
        /// Gets all discovered verb types
        /// </summary>
        public static IReadOnlyList<Type> Verbs
        {
            get
            {
                if (_verbs == null)
                {
                    _verbs = [.. Words
                        .Where(x => typeof(IVerb).IsAssignableFrom(x))];
                }
                return _verbs;
            }
        }

        /// <summary>
        /// Gets all discovered noun types
        /// </summary>
        public static IReadOnlyList<Type> Nouns
        {
            get
            {
                if (_nouns == null)
                {
                    _nouns = [.. Words
                        .Where(x => typeof(INoun).IsAssignableFrom(x))];
                }
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