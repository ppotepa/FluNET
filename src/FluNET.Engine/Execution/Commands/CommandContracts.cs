using FluNET.Language.Binding;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Execution.Commands;

/// <summary>A typed command whose execution produces <typeparamref name="TResult"/>.</summary>
public interface ICommand<out TResult>
{
}

/// <summary>Executes one typed command without reflecting over verb constructors.</summary>
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    /// <remarks>
    /// Handlers registered in one runtime may be invoked concurrently for
    /// independent AND branches. Mutable handler state must therefore be
    /// synchronized or scoped behind an injected capability.
    /// </remarks>
    ValueTask<TResult> HandleAsync(
        TCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>Binds a semantic frame to a typed command, or declines the frame.</summary>
public interface ICommandBinder<TCommand, TResult>
    where TCommand : class, ICommand<TResult>
{
    TCommand? TryBind(BoundCommand command);
}

/// <summary>
/// Type-erased route used only by orchestration. Binding and handling stay
/// fully generic on either side of this boundary.
/// </summary>
public interface ICommandRoute
{
    bool CanHandle(BoundCommand command);

    ValueTask<CommandDispatchResult> TryExecuteAsync(
        BoundCommand command,
        CancellationToken cancellationToken = default);
}

public readonly record struct CommandDispatchResult(bool IsHandled, object? Result)
{
    public static CommandDispatchResult NotHandled => new(false, null);
    public static CommandDispatchResult Handled(object? result) => new(true, result);
}

public sealed class CommandRoute<TCommand, TResult>(
    ICommandBinder<TCommand, TResult> binder,
    ICommandHandler<TCommand, TResult> handler) : ICommandRoute
    where TCommand : class, ICommand<TResult>
{
    public bool CanHandle(BoundCommand command) => binder.TryBind(command) is not null;

    public async ValueTask<CommandDispatchResult> TryExecuteAsync(
        BoundCommand command,
        CancellationToken cancellationToken = default)
    {
        TCommand? typedCommand = binder.TryBind(command);
        if (typedCommand is null)
        {
            return CommandDispatchResult.NotHandled;
        }

        TResult result = await handler.HandleAsync(typedCommand, cancellationToken).ConfigureAwait(false);
        return CommandDispatchResult.Handled(result);
    }
}

/// <summary>Tries typed routes in registration order before legacy execution.</summary>
public sealed class CommandDispatcher(IEnumerable<ICommandRoute> routes)
{
    private readonly IReadOnlyList<ICommandRoute> _routes = routes.ToArray();

    public bool CanDispatch(BoundCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return _routes.Any(route => route.CanHandle(command));
    }

    public async ValueTask<CommandDispatchResult> TryExecuteAsync(
        BoundCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (ICommandRoute route in _routes)
        {
            CommandDispatchResult result = await route.TryExecuteAsync(command, cancellationToken)
                .ConfigureAwait(false);
            if (result.IsHandled)
            {
                return result;
            }
        }

        return CommandDispatchResult.NotHandled;
    }
}

public static class TypedCommandServiceCollectionExtensions
{
    public static IServiceCollection AddTypedCommand<TCommand, TResult, TBinder, THandler>(
        this IServiceCollection services)
        where TCommand : class, ICommand<TResult>
        where TBinder : class, ICommandBinder<TCommand, TResult>
        where THandler : class, ICommandHandler<TCommand, TResult>
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTransient<ICommandBinder<TCommand, TResult>, TBinder>();
        services.AddTransient<ICommandHandler<TCommand, TResult>, THandler>();
        services.AddTransient<ICommandRoute, CommandRoute<TCommand, TResult>>();
        return services;
    }
}
