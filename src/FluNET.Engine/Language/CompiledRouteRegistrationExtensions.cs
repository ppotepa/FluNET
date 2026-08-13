using FluNET.Execution.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Language;

/// <summary>
/// Namespace-local route registration used by FluNetModuleBuilder. Keeping the
/// source-level AddTypedCommand name preserves the builder contract while module
/// routes use the 0.4 compiled-command cache. The older Execution.Commands
/// extension remains available to direct 0.3 DI integrations.
/// </summary>
internal static class CompiledRouteRegistrationExtensions
{
    public static IServiceCollection AddTypedCommand<TCommand, TResult, TBinder, THandler>(
        this IServiceCollection services,
        FrameId frameId)
        where TCommand : class, ICommand<TResult>
        where TBinder : class, ICommandBinder<TCommand, TResult>
        where THandler : class, ICommandHandler<TCommand, TResult> =>
        CompiledCommandRouteServiceCollectionExtensions
            .AddCompiledTypedCommand<TCommand, TResult, TBinder, THandler>(services, frameId);
}
