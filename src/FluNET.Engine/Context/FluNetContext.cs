using FluNET.Language;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Context;

public class FluNETContext : IDisposable
{
    private static readonly object _defaultContextGate = new();
    private static FluNETContext? _defaultContext;
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScope? _scope;

    [Obsolete("Prefer FluNETContext.Create() or CreateWithRuntime() with explicit disposal. Default is retained for pre-1.0 compatibility.")]
    public static FluNETContext Default
    {
        get
        {
            lock (_defaultContextGate)
            {
                return _defaultContext ??= Create();
            }
        }
    }

    private FluNETContext(ServiceProvider serviceProvider, bool createScope = true)
    {
        _serviceProvider = serviceProvider;
        if (createScope)
            _scope = _serviceProvider.CreateScope();
    }

    public static FluNETContext Create(Action<IServiceCollection>? configure = null)
    {
        ServiceCollection services = new();
        ConfigureDefaultServices(services, StandardLanguage.CreateRuntime());
        configure?.Invoke(services);
        return new FluNETContext(BuildServiceProvider(services));
    }

    public static FluNETContext CreateWithRuntime(
        FluNetRuntimeDefinition runtime,
        Action<IServiceCollection>? configure = null)
    {
        ServiceCollection services = new();
        ConfigureDefaultServices(services, runtime);
        configure?.Invoke(services);
        return new FluNETContext(BuildServiceProvider(services));
    }

    public static void ConfigureDefaultServices(IServiceCollection services) =>
        ConfigureDefaultServices(services, StandardLanguage.CreateRuntime());

    public static void ConfigureDefaultServices(
        IServiceCollection services,
        FluNetRuntimeDefinition runtime)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(runtime);
        FluNetDefaultServiceRegistration.Configure(services, runtime);
    }

    public Engine GetEngine() => GetService<Engine>();

    public T GetService<T>() where T : notnull =>
        _scope is not null
            ? _scope.ServiceProvider.GetRequiredService<T>()
            : _serviceProvider.GetRequiredService<T>();

    public object GetService(Type type) =>
        _scope is not null
            ? _scope.ServiceProvider.GetRequiredService(type)
            : _serviceProvider.GetRequiredService(type);

    public IServiceProvider ServiceProvider => _scope?.ServiceProvider ?? _serviceProvider;

    public void Dispose()
    {
        lock (_defaultContextGate)
        {
            if (ReferenceEquals(this, _defaultContext))
                _defaultContext = null;
        }

        _scope?.Dispose();
        _serviceProvider.Dispose();
    }

    public static void ResetDefault()
    {
        FluNETContext? context;
        lock (_defaultContextGate)
        {
            context = _defaultContext;
            _defaultContext = null;
        }

        context?.Dispose();
    }

    private static ServiceProvider BuildServiceProvider(IServiceCollection services) =>
        services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
}
