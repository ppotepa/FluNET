using FluNET.Compilation;
using FluNET.Language;
using FluNET.Language.Binding;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Execution.Commands;

/// <summary>
/// Canonical 0.4 route: bind once during compilation, cache by immutable
/// BoundCommand identity, and invoke only the handler during execution.
/// </summary>
public sealed class CompiledCommandRoute<TCommand, TResult> : ICommandRoute
    where TCommand : class, ICommand<TResult>
{
    private readonly ICommandBinder<TCommand, TResult> _binder;
    private readonly ICommandHandler<TCommand, TResult> _handler;

    public CompiledCommandRoute(
        FrameId frameId,
        ICommandBinder<TCommand, TResult> binder,
        ICommandHandler<TCommand, TResult> handler)
    {
        if (frameId.IsEmpty)
        {
            throw new ArgumentException("A frame id is required.", nameof(frameId));
        }
        FrameId = frameId;
        _binder = binder ?? throw new ArgumentNullException(nameof(binder));
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public FrameId? FrameId { get; }

    public bool CanHandle(BoundCommand command) =>
        command is not null && command.Frame.Id == FrameId;

    public CompiledCommand? TryCompile(BoundCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Frame.Id != FrameId)
        {
            return null;
        }
        if (CompiledCommandCache.TryGet(command, out CompiledCommand? cached))
        {
            return cached;
        }

        TCommand? value = _binder.TryBind(command);
        if (value is null)
        {
            return null;
        }

        CompiledCommand compiled = new(
            command,
            value,
            typeof(TCommand),
            typeof(TResult),
            command.Frame.ResultTypeSymbol);
        return CompiledCommandCache.Set(command, compiled);
    }

    public async ValueTask<CommandDispatchResult> TryExecuteCompiledAsync(
        CompiledCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.FrameId != FrameId || command.Value is not TCommand typed)
        {
            return CommandDispatchResult.NotHandled;
        }

        TResult result = await _handler.HandleAsync(typed, cancellationToken)
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

public static class CompiledCommandRouteServiceCollectionExtensions
{
    public static IServiceCollection AddCompiledTypedCommand<TCommand, TResult, TBinder, THandler>(
        this IServiceCollection services,
        FrameId frameId)
        where TCommand : class, ICommand<TResult>
        where TBinder : class, ICommandBinder<TCommand, TResult>
        where THandler : class, ICommandHandler<TCommand, TResult>
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTransient<ICommandBinder<TCommand, TResult>, TBinder>();
        services.AddTransient<ICommandHandler<TCommand, TResult>, THandler>();
        services.AddTransient<ICommandRoute>(provider =>
            new CompiledCommandRoute<TCommand, TResult>(
                frameId,
                provider.GetRequiredService<ICommandBinder<TCommand, TResult>>(),
                provider.GetRequiredService<ICommandHandler<TCommand, TResult>>()));
        return services;
    }
}
