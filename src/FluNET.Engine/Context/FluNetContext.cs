using FluNET.Language;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Context;

public class FluNETContext : IDisposable, IAsyncDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScope? _scope;
    private int _disposed;

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

    public T GetService<T>() where T : notnull
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _scope is not null
            ? _scope.ServiceProvider.GetRequiredService<T>()
            : _serviceProvider.GetRequiredService<T>();
    }

    public object GetService(Type type)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _scope is not null
            ? _scope.ServiceProvider.GetRequiredService(type)
            : _serviceProvider.GetRequiredService(type);
    }

    public IServiceProvider ServiceProvider
    {
        get
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            return _scope?.ServiceProvider ?? _serviceProvider;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _scope?.Dispose();
        _serviceProvider.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        if (_scope is IAsyncDisposable asyncScope)
            await asyncScope.DisposeAsync().ConfigureAwait(false);
        else
            _scope?.Dispose();
        await _serviceProvider.DisposeAsync().ConfigureAwait(false);
    }

    private static ServiceProvider BuildServiceProvider(IServiceCollection services) =>
        services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
}
