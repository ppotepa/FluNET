<<<<<<< HEAD
using FluNET.Capabilities;using FluNET.Compatibility;using FluNET.Compilation;using FluNET.Execution.Commands;using FluNET.Execution.Planning;using FluNET.Execution.Workflow;using FluNET.Extensions;using FluNET.Language;using FluNET.Language.Binding;using FluNET.Sentences;using FluNET.Syntax.Registry;using FluNET.Syntax.Validation;using FluNET.Tokens;using FluNET.Tokens.Tree;using FluNET.Variables;using FluNET.Words;using Microsoft.Extensions.DependencyInjection;namespace FluNET.Context{public class FluNETContext:IDisposable{private static FluNETContext?_defaultContext;private readonly ServiceProvider sp;private readonly IServiceScope?scope;public static FluNETContext Default=>_defaultContext??=Create();private FluNETContext(ServiceProvider s,bool createScope=true){sp=s;if(createScope)scope=sp.CreateScope();}public static FluNETContext Create(Action<IServiceCollection>?c=null){var s=new ServiceCollection();ConfigureDefaultServices(s,StandardLanguage.CreateRuntime());c?.Invoke(s);return new FluNETContext(s.BuildServiceProvider());}public static FluNETContext CreateWithRuntime(FluNetRuntimeDefinition r,Action<IServiceCollection>?c=null){var s=new ServiceCollection();ConfigureDefaultServices(s,r);c?.Invoke(s);return new FluNETContext(s.BuildServiceProvider());}public static void ConfigureDefaultServices(IServiceCollection s)=>ConfigureDefaultServices(s,StandardLanguage.CreateRuntime());public static void ConfigureDefaultServices(IServiceCollection s,FluNetRuntimeDefinition r){s.AddSingleton(r.Language);s.AddSingleton<LanguageRegistry>();s.AddSingleton<DiscoveryService>();s.AddSingleton<SemanticCommandBinder>();s.AddSingleton<IExecutionPolicy,AllowAllExecutionPolicy>();s.AddSingleton<IFluNetFileSystem,PhysicalFluNetFileSystem>();s.AddSingleton(new HttpClient{Timeout=TimeSpan.FromMinutes(5)});s.AddSingleton<IHttpTransport,HttpTransport>();s.AddSingleton<IHttpAuthenticationScheme,BearerHttpAuthenticationScheme>();s.AddSingleton<IAuthenticatedHttpTransport,AuthenticatedHttpTransport>();s.AddSingleton<ISqlQueryExecutor,DenySqlQueryExecutor>();s.AddSingleton<ITextOutput,ConsoleTextOutput>();s.AddSingleton<IEmailTransport,DiagnosticEmailTransport>();s.AddSingleton<IWorkflowStateStore,InMemoryWorkflowStateStore>();s.AddSingleton<IWorkflowValueSerializer,JsonWorkflowValueSerializer>();s.AddSingleton<IExecutionResultCache,InMemoryExecutionResultCache>();s.AddSingleton<IIdempotencyStore,InMemoryIdempotencyStore>();s.AddSingleton<ISecretStore,EmptySecretStore>();s.AddSingleton<ISecretAccessPolicy,DenyAllSecretAccessPolicy>();s.AddTransient<TokenFactory>();s.AddTransient<TokenTreeFactory>();s.AddTransient<WordFactory>();s.AddTransient<Lexicon.Lexicon>();s.AddTransient<SentenceValidator>();s.AddSingleton<VerbRegistry>();s.AddTransient<SentenceFactory>();s.AddTransient<SentenceExecutor>();s.AddTransient<LegacySentenceAdapter>();r.RegisterRoutes(s);s.AddTransient<CommandDispatcher>();s.AddTransient<TypedProgramCompiler>();s.AddTransient<TypedProgramTypeValidator>();s.AddSingleton<ExecutionPlanner>();s.AddTransient<ExecutionPlanExecutor>();s.AddPatternMatchers();s.AddScoped<IVariableResolver,VariableResolver>();s.AddTransient<Execution.ExecutionPipelineFactory>();s.AddTransient<Engine>(p=>new Engine(p.GetRequiredService<IVariableResolver>(),p.GetRequiredService<Execution.ExecutionPipelineFactory>(),p.GetRequiredService<SemanticCommandBinder>(),p.GetRequiredService<ExecutionPlanner>(),p.GetRequiredService<LanguageSnapshot>(),p.GetRequiredService<LegacySentenceAdapter>()));}public Engine GetEngine()=>GetService<Engine>();public T GetService<T>()where T:notnull=>scope!=null?scope.ServiceProvider.GetRequiredService<T>():sp.GetRequiredService<T>();public object GetService(Type t)=>scope!=null?scope.ServiceProvider.GetRequiredService(t):sp.GetRequiredService(t);public IServiceProvider ServiceProvider=>scope?.ServiceProvider??sp;public void Dispose(){scope?.Dispose();if(this==_defaultContext)_defaultContext=null;sp.Dispose();}public static void ResetDefault(){_defaultContext?.Dispose();_defaultContext=null;}}}
=======
using FluNET.Extensions;
using FluNET.Sentences;
using FluNET.Syntax.Validation;
using FluNET.Tokens;
using FluNET.Tokens.Tree;
using FluNET.Variables;
using FluNET.Words;
using FluNET.Syntax.Registry;
using FluNET.Capabilities;
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
            // One deterministic language registry shared by discovery, parsing, and execution
            services.AddSingleton<LanguageRegistry>();
            services.AddSingleton<DiscoveryService>();

            // External effects are explicit capabilities and can be replaced by hosts/tests
            services.AddSingleton<IExecutionPolicy, AllowAllExecutionPolicy>();
            services.AddSingleton<IFluNetFileSystem, PhysicalFluNetFileSystem>();
            services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromMinutes(5) });
            services.AddSingleton<IHttpTransport, HttpTransport>();
            services.AddSingleton<ITextOutput, ConsoleTextOutput>();

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
>>>>>>> origin/agent/stabilize-poc-foundation
