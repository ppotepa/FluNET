using FluNET.Syntax.Core;
using System.Reflection;

namespace FluNET.Syntax.Registry
{
    /// <summary>
    /// Registry for dynamically discovering and creating verb instances.
    /// Similar to how MVC discovers controllers at runtime.
    /// </summary>
    public class VerbRegistry
    {
        private readonly Dictionary<string, Func<IVerb>> _verbFactories = new(StringComparer.OrdinalIgnoreCase);
        private readonly IServiceProvider _serviceProvider;

        public VerbRegistry(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            DiscoverVerbs();
        }

        /// <summary>
        /// Discovers all verb types in loaded assemblies using reflection (done once at startup)
        /// </summary>
        private void DiscoverVerbs()
        {
            var verbTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(IVerb).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

            foreach (var verbType in verbTypes)
            {
                RegisterVerbType(verbType);
            }
        }

        /// <summary>
        /// Registers a verb type and creates a factory function for it
        /// </summary>
        private void RegisterVerbType(Type verbType)
        {
            try
            {
                // Create factory function for this verb type
                Func<IVerb> factory = () => (_serviceProvider.GetService(verbType) as IVerb) ??
                                            (IVerb)Activator.CreateInstance(verbType)!;

                // Create temporary instance to get name and synonyms
                var tempInstance = factory();
                _verbFactories[tempInstance.Text] = factory;

                // Register synonyms
                foreach (var synonym in tempInstance.Synonyms)
                {
                    _verbFactories[synonym] = factory;
                }
            }
            catch (Exception)
            {
                // Skip verbs that can't be instantiated
            }
        }

        /// <summary>
        /// Gets a verb instance by name (dynamically discovered)
        /// </summary>
        public IVerb GetVerbByName(string name)
        {
            if (_verbFactories.TryGetValue(name, out var factory))
            {
                return factory();
            }

            throw new VerbNotFoundException($"No verb found with name '{name}'");
        }

        /// <summary>
        /// Gets all registered verb names
        /// </summary>
        public IEnumerable<string> GetAllVerbNames() => _verbFactories.Keys;

        /// <summary>
        /// Gets the count of registered verbs
        /// </summary>
        public int Count => _verbFactories.Count;
    }

    /// <summary>
    /// Exception thrown when a verb is not found in the registry
    /// </summary>
    public class VerbNotFoundException : Exception
    {
        public VerbNotFoundException(string message) : base(message) { }
    }
}
