using FluNET.Extensions;
using FluNET.Sentences;
using FluNET.Syntax.Validation;
using FluNET.Tokens;
using FluNET.Tokens.Tree;
using FluNET.Variables;
using FluNET.Words;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Context
{
    /// <summary>
    /// Centralized service configuration and resolution for FluNET applications.
    /// Provides a single source of truth for dependency injection setup across
    /// CLI applications, web applications, and tests.
    /// </summary>
    public class FluNETContext : IDisposable
    {
        private static FluNETContext? _defaultContext;
        private readonly ServiceProvider _serviceProvider;
        private readonly IServiceScope? _scope;

        /// <summary>
        /// Gets the global default context with standard configuration.
        /// Lazily creates the context on first access.
        /// </summary>
        public static FluNETContext Default => _defaultContext ??= Create();

        private FluNETContext(ServiceProvider serviceProvider, bool createScope = true)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            if (createScope)
            {
                _scope = _serviceProvider.CreateScope();
            }
        }

        /// <summary>
        /// Creates a new FluNETContext with standard configuration.
        /// Optionally allows additional service registration.
        /// </summary>
        /// <param name="configureServices">Optional callback to add or override services.
        /// Called AFTER default services are registered, so you can override defaults.</param>
        /// <returns>A new FluNETContext instance</returns>
        public static FluNETContext Create(Action<IServiceCollection>? configureServices = null)
        {
            var services = new ServiceCollection();
            ConfigureDefaultServices(services);
            configureServices?.Invoke(services);
            return new FluNETContext(services.BuildServiceProvider());
        }

        /// <summary>
        /// Configures all default FluNET services.
        /// THIS IS THE SINGLE SOURCE OF TRUTH for service registration.
        /// Any changes to dependencies should be made here.
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        public static void ConfigureDefaultServices(IServiceCollection services)
        {
            // Discovery services
            services.AddTransient<DiscoveryService>();

            // Token processing
            services.AddTransient<TokenFactory>();
            services.AddTransient<TokenTreeFactory>();

            // Word processing
            services.AddTransient<WordFactory>();

            // Lexicon and validation
            services.AddTransient<Lexicon.Lexicon>();
            services.AddTransient<SentenceValidator>();

            // Verb registry for dynamic verb discovery
            services.AddSingleton<Syntax.Registry.VerbRegistry>();

            // Sentence processing
            services.AddTransient<SentenceFactory>();
            services.AddTransient<SentenceExecutor>();

            // Pattern matchers (regex and string-based implementations)
            services.AddPatternMatchers();

            // Variable resolution (scoped to maintain state within execution context)
            services.AddScoped<IVariableResolver, VariableResolver>();

            // Execution pipeline (modular execution architecture)
            services.AddTransient<Execution.ExecutionPipelineFactory>();

            // Engine (main entry point)
            services.AddTransient<Engine>();
        }

        /// <summary>
        /// Gets the FluNET engine instance from the context.
        /// </summary>
        /// <returns>A configured Engine instance</returns>
        public Engine GetEngine() => GetService<Engine>();

        /// <summary>
        /// Resolves a service from the context.
        /// </summary>
        /// <typeparam name="T">The service type to resolve</typeparam>
        /// <returns>The resolved service instance</returns>
        public T GetService<T>() where T : notnull
        {
            return _scope != null
                ? _scope.ServiceProvider.GetRequiredService<T>()
                : _serviceProvider.GetRequiredService<T>();
        }

        /// <summary>
        /// Resolves a service from the context by type.
        /// </summary>
        /// <param name="serviceType">The type of service to resolve</param>
        /// <returns>The resolved service instance</returns>
        public object GetService(Type serviceType)
        {
            return _scope != null
                ? _scope.ServiceProvider.GetRequiredService(serviceType)
                : _serviceProvider.GetRequiredService(serviceType);
        }

        /// <summary>
        /// Gets the underlying service provider.
        /// Useful for advanced scenarios or framework integration.
        /// </summary>
        public IServiceProvider ServiceProvider => _scope?.ServiceProvider ?? _serviceProvider;

        /// <summary>
        /// Disposes the context and all managed resources.
        /// </summary>
        public void Dispose()
        {
            _scope?.Dispose();

            // Clear default context reference if this is the default
            if (this == _defaultContext)
            {
                _defaultContext = null;
            }

            _serviceProvider?.Dispose();
        }

        /// <summary>
        /// Resets the default context. Useful for testing scenarios.
        /// </summary>
        public static void ResetDefault()
        {
            _defaultContext?.Dispose();
            _defaultContext = null;
        }
    }
}
