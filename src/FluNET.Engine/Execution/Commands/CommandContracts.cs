using FluNET.Compilation;
using FluNET.Language;
using FluNET.Language.Binding;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Execution.Commands;

public interface ICommand<out TResult>
{
}

public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    ValueTask<TResult> HandleAsync(
        TCommand command,
        CancellationToken cancellationToken = default);
}

public interface ICommandBinder<TCommand, TResult>
    where TCommand : class, ICommand<TResult>
{
    TCommand? TryBind(BoundCommand command);
}

/// <summary>Type-erased route that separates compilation from execution.</summary>
public interface ICommandRoute
{
    FrameId? FrameId { get; }

    bool CanHandle(BoundCommand command);

    /// <summary>Canonical compile-time hook. Legacy custom routes may leave the default.</summary>
    CompiledCommand? TryCompile(BoundCommand command) => null;

    /// <summary>Canonical execution hook for an already-bound command.</summary>
    ValueTask<CommandDispatchResult> TryExecuteCompiledAsync(
        CompiledCommand command,
        CancellationToken cancellationToken = default) =>
        TryExecuteAsync(command.Source, cancellationToken);

    /// <summary>Compatibility hook for pre-0.4 route implementations.</summary>
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
        if (FrameId is { } frameId && command.Frame.Id != frameId)
        {
            return false;
        }
        return _binder.TryBind(command) is not null;
    }

    public CompiledCommand? TryCompile(BoundCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (FrameId is { } frameId && command.Frame.Id != frameId)
        {
            return null;
        }

        TCommand? typedCommand = _binder.TryBind(command);
        return typedCommand is null
            ? null
            : new CompiledCommand(
                command,
                typedCommand,
                typeof(TCommand),
                typeof(TResult),
                command.Frame.ResultTypeSymbol);
    }

    public async ValueTask<CommandDispatchResult> TryExecuteCompiledAsync(
        CompiledCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (FrameId is { } frameId && command.FrameId != frameId)
        {
            return CommandDispatchResult.NotHandled;
        }
        if (command.Value is not TCommand typedCommand)
        {
            return CommandDispatchResult.NotHandled;
        }

        TResult result = await _handler.HandleAsync(typedCommand, cancellationToken)
            .ConfigureAwait(false);
        return CommandDispatchResult.Handled(result);
    }

    public async ValueTask<CommandDispatchResult> TryExecuteAsync(
        BoundCommand command,
        CancellationToken cancellationToken = default)
    {
        CompiledCommand? compiled = TryCompile(command);
        return compiled is null
            ? CommandDispatchResult.NotHandled
            : await TryExecuteCompiledAsync(compiled, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class CommandDispatcher(IEnumerable<ICommandRoute> routes)
{
    private readonly IReadOnlyList<ICommandRoute> _routes = routes.ToArray();

    public bool CanDispatch(BoundCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return MatchingRoutes(command.Frame.Id).Any(route => route.CanHandle(command));
    }

    /// <summary>Binds one command exactly once for the canonical compiler pipeline.</summary>
    public CompiledCommand? TryCompile(BoundCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        foreach (ICommandRoute route in MatchingRoutes(command.Frame.Id))
        {
            CompiledCommand? compiled = route.TryCompile(command);
            if (compiled is not null)
            {
                return compiled;
            }
        }
        return null;
    }

    public async ValueTask<CommandDispatchResult> TryExecuteAsync(
        CompiledCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        foreach (ICommandRoute route in MatchingRoutes(command.FrameId))
        {
            CommandDispatchResult result = await route
                .TryExecuteCompiledAsync(command, cancellationToken)
                .ConfigureAwait(false);
            if (result.IsHandled)
            {
                return result;
            }
        }
        return CommandDispatchResult.NotHandled;
    }

    /// <summary>Compatibility adapter: compile once and immediately execute.</summary>
    public async ValueTask<CommandDispatchResult> TryExecuteAsync(
        BoundCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        CompiledCommand? compiled = TryCompile(command);
        if (compiled is not null)
        {
            return await TryExecuteAsync(compiled, cancellationToken).ConfigureAwait(false);
        }

        // Preserve custom ICommandRoute implementations from the 0.3 API.
        foreach (ICommandRoute route in MatchingRoutes(command.Frame.Id))
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

    private IEnumerable<ICommandRoute> MatchingRoutes(FrameId frameId)
    {
        ICommandRoute[] exact = _routes
            .Where(route => route.FrameId is { } id && id == frameId)
            .ToArray();
        return exact.Length > 0
            ? exact
            : _routes.Where(route => route.FrameId is null);
    }
}

public static class TypedCommandServiceCollectionExtensions
{
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
