using FluNET.Syntax.Core;
using FluNET.Syntax.Nouns;
using FluNET.Syntax.Verbs;
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
        private readonly Dictionary<Type, VerbMetadata> _verbMetadata = new();
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Metadata about a verb type for efficient construction
        /// </summary>
        private record VerbMetadata(
            Type VerbType,
            ConstructorInfo ParameterizedConstructor,
            bool HasWhat,
            bool HasFrom,
            bool HasTo,
            bool HasUsing
        );

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

                // Store metadata for parameterized construction
                StoreVerbMetadata(verbType);
            }
            catch (Exception)
            {
                // Skip verbs that can't be instantiated
            }
        }

        /// <summary>
        /// Stores metadata about a verb type for efficient parameterized construction
        /// </summary>
        private void StoreVerbMetadata(Type verbType)
        {
            // Find the constructor with parameters (skip parameterless constructor)
            var constructors = verbType.GetConstructors()
                .Where(c => c.GetParameters().Length > 0)
                .OrderByDescending(c => c.GetParameters().Length)
                .ToArray();

            if (constructors.Length == 0)
            {
                return; // No parameterized constructor
            }

            var constructor = constructors[0];

            // Analyze what parameters this verb needs
            Type[] interfaces = verbType.GetInterfaces();
            bool hasWhat = interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWhat<>));
            bool hasFrom = interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IFrom<>));
            bool hasTo = interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITo<>));
            bool hasUsing = interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IUsing<>));

            _verbMetadata[verbType] = new VerbMetadata(
                verbType,
                constructor,
                hasWhat,
                hasFrom,
                hasTo,
                hasUsing
            );
        }

        /// <summary>
        /// Creates a verb instance with the provided parameters.
        /// Parameters should be in order: WHAT, FROM, TO, USING (only include what the verb needs).
        /// </summary>
        public object? CreateVerbInstance(Type verbType, params object?[] parameters)
        {
            if (!_verbMetadata.TryGetValue(verbType, out var metadata))
            {
                // Fallback to parameterless construction
                return Activator.CreateInstance(verbType);
            }

            try
            {
                return metadata.ParameterizedConstructor.Invoke(parameters);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create {verbType.Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets metadata about a verb type's parameter requirements
        /// </summary>
        public (bool HasWhat, bool HasFrom, bool HasTo, bool HasUsing) GetVerbParameterInfo(Type verbType)
        {
            if (_verbMetadata.TryGetValue(verbType, out var metadata))
            {
                return (metadata.HasWhat, metadata.HasFrom, metadata.HasTo, metadata.HasUsing);
            }

            return (false, false, false, false);
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
