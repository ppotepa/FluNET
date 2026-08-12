using FluNET.Language;
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

/// <summary>Type-erased executable route selected by stable frame identity.</summary>
public interface ICommandRoute
{
    /// <summary>
    /// Stable frame id for canonical routes. Null is reserved for the legacy
    /// direct-DI registration overload retained for source compatibility.
    /// </summary>
    FrameId? FrameId { get; }

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

public sealed class CommandRoute<TCommand, TResult> : ICommandRoute
    where TCommand : class, ICommand<TResult>
{
    private readonly ICommandBinder<TCommand, TResult> _binder;
    private readonly ICommandHandler<TCommand, TResult> _handler;

    /// <summary>Compatibility constructor for direct route construction.</summary>
    public CommandRoute(
        ICommandBinder<TCommand, TResult> binder,
        ICommandHandler<TCommand, TResult> handler)
        : this(null, binder, handler)
    {
    }

    public CommandRoute(
        FrameId? frameId,
        ICommandBinder<TCommand, TResult> binder,
        ICommandHandler<TCommand, TResult> handler)
    {
        FrameId = frameId;
        _binder = binder ?? throw new ArgumentNullException(nameof(binder));
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public FrameId? FrameId { get; }

    public bool CanHandle(BoundCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return (FrameId is null || command.Frame.Id == FrameId.Value) &&
            _binder.TryBind(command) is not null;
    }

    public async ValueTask<CommandDispatchResult> TryExecuteAsync(
        BoundCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (FrameId is { } frameId && command.Frame.Id != frameId)
        {
            return CommandDispatchResult.NotHandled;
        }

        TCommand? typedCommand = _binder.TryBind(command);
        if (typedCommand is null)
        {
            return CommandDispatchResult.NotHandled;
        }

        TResult result = await _handler.HandleAsync(typedCommand, cancellationToken).ConfigureAwait(false);
        return CommandDispatchResult.Handled(result);
    }
}

/// <summary>Dispatches typed handlers by stable frame id, preserving registration order as a fallback.</summary>
public sealed class CommandDispatcher(IEnumerable<ICommandRoute> routes)
{
    private readonly IReadOnlyList<ICommandRoute> _routes = routes.ToArray();

    public bool CanDispatch(BoundCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return MatchingRoutes(command).Any(route => route.CanHandle(command));
    }

    public async ValueTask<CommandDispatchResult> TryExecuteAsync(
        BoundCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (ICommandRoute route in MatchingRoutes(command))
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

    private IEnumerable<ICommandRoute> MatchingRoutes(BoundCommand command)
    {
        ICommandRoute[] exact = _routes
            .Where(route => route.FrameId is { } frameId && frameId == command.Frame.Id)
            .ToArray();
        return exact.Length > 0
            ? exact
            : _routes.Where(route => route.FrameId is null);
    }
}

public static class TypedCommandServiceCollectionExtensions
{
    /// <summary>Registers a typed route for one stable semantic frame.</summary>
    public static IServiceCollection AddTypedCommand<TCommand, TResult, TBinder, THandler>(
        this IServiceCollection services,
        FrameId frameId)
        where TCommand : class, ICommand<TResult>
        where TBinder : class, ICommandBinder<TCommand, TResult>
        where THandler : class, ICommandHandler<TCommand, TResult>
    {
        ArgumentNullException.ThrowIfNull(services);
        if (frameId.IsEmpty)
        {
            throw new ArgumentException("A frame id is required.", nameof(frameId));
        }

        services.AddTransient<ICommandBinder<TCommand, TResult>, TBinder>();
        services.AddTransient<ICommandHandler<TCommand, TResult>, THandler>();
        services.AddTransient<ICommandRoute>(provider =>
            new CommandRoute<TCommand, TResult>(
                frameId,
                provider.GetRequiredService<ICommandBinder<TCommand, TResult>>(),
                provider.GetRequiredService<ICommandHandler<TCommand, TResult>>()));
        return services;
    }

    /// <summary>
    /// Compatibility registration for callers that construct ICommandRoute
    /// directly through DI without a language runtime definition.
    /// </summary>
    public static IServiceCollection AddTypedCommand<TCommand, TResult, TBinder, THandler>(
        this IServiceCollection services)
        where TCommand : class, ICommand<TResult>
        where TBinder : class, ICommandBinder<TCommand, TResult>
        where THandler : class, ICommandHandler<TCommand, TResult>
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTransient<ICommandBinder<TCommand, TResult>, TBinder>();
        services.AddTransient<ICommandHandler<TCommand, TResult>, THandler>();
        services.AddTransient<ICommandRoute>(provider =>
            new CommandRoute<TCommand, TResult>(
                null,
                provider.GetRequiredService<ICommandBinder<TCommand, TResult>>(),
                provider.GetRequiredService<ICommandHandler<TCommand, TResult>>()));
        return services;
    }
}
