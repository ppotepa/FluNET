using FluNET.Language;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Context;

public class FluNETContext : IDisposable
{
    private static readonly object DefaultContextGate = new();
    private static FluNETContext? _defaultContext;
    private readonly ServiceProvider sp;
    private readonly IServiceScope? scope;

    public static FluNETContext Default
    {
        get
        {
            lock (DefaultContextGate)
            {
                return _defaultContext ??= Create();
            }
        }
    }

    private FluNETContext(ServiceProvider serviceProvider, bool createScope = true)
    {
        sp = serviceProvider;
        if (createScope)
            scope = sp.CreateScope();
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
        scope is not null
            ? scope.ServiceProvider.GetRequiredService<T>()
            : sp.GetRequiredService<T>();

    public object GetService(Type type) =>
        scope is not null
            ? scope.ServiceProvider.GetRequiredService(type)
            : sp.GetRequiredService(type);

    public IServiceProvider ServiceProvider => scope?.ServiceProvider ?? sp;

    public void Dispose()
    {
        lock (DefaultContextGate)
        {
            if (ReferenceEquals(this, _defaultContext))
                _defaultContext = null;
        }

        scope?.Dispose();
        sp.Dispose();
    }

    public static void ResetDefault()
    {
        FluNETContext? context;
        lock (DefaultContextGate)
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
