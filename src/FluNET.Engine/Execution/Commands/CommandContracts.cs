using FluNET.Compilation;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Variables;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Execution.Commands;

public interface ICommand<out TResult> { }
public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult> { ValueTask<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default); }
public interface ICommandBinder<TCommand, TResult> where TCommand : class, ICommand<TResult> { TCommand? TryBind(BoundCommand command); }
public interface ICommandRoute
{
    FrameId? FrameId { get; }
    bool CanHandle(BoundCommand command);
    CompiledCommand? TryCompile(BoundCommand command) => null;
    ValueTask<CommandDispatchResult> TryExecuteCompiledAsync(CompiledCommand command, CancellationToken cancellationToken = default) => TryExecuteAsync(command.Source, cancellationToken);
    ValueTask<CommandDispatchResult> TryExecuteAsync(BoundCommand command, CancellationToken cancellationToken = default);
}
public readonly record struct CommandDispatchResult(bool IsHandled, object? Result)
{ public static CommandDispatchResult NotHandled => new(false, null); public static CommandDispatchResult Handled(object? result) => new(true, result); }

public sealed class CommandRoute<TCommand, TResult> : ICommandRoute where TCommand : class, ICommand<TResult>
{
    private readonly ICommandBinder<TCommand, TResult> _binder; private readonly ICommandHandler<TCommand, TResult> _handler;
    public CommandRoute(ICommandBinder<TCommand, TResult> binder, ICommandHandler<TCommand, TResult> handler) : this(null, binder, handler) { }
    public CommandRoute(FrameId? frameId, ICommandBinder<TCommand, TResult> binder, ICommandHandler<TCommand, TResult> handler) { FrameId = frameId; _binder = binder; _handler = handler; }
    public FrameId? FrameId { get; }
    public bool CanHandle(BoundCommand command) => (FrameId is null || FrameId == command.Frame.Id) && _binder.TryBind(command) is not null;
    public CompiledCommand? TryCompile(BoundCommand command)
    { if (FrameId is { } id && command.Frame.Id != id) return null; TCommand? typed = _binder.TryBind(command); return typed is null ? null : new CompiledCommand(command, typed, typeof(TCommand), typeof(TResult), command.Frame.ResultTypeSymbol); }
    public async ValueTask<CommandDispatchResult> TryExecuteCompiledAsync(CompiledCommand command, CancellationToken cancellationToken = default)
    { if (FrameId is { } id && command.FrameId != id) return CommandDispatchResult.NotHandled; if (command.Value is not TCommand typed) return CommandDispatchResult.NotHandled; return CommandDispatchResult.Handled(await _handler.HandleAsync(typed, cancellationToken).ConfigureAwait(false)); }
    public async ValueTask<CommandDispatchResult> TryExecuteAsync(BoundCommand command, CancellationToken cancellationToken = default)
    { CompiledCommand? compiled = TryCompile(command); return compiled is null ? CommandDispatchResult.NotHandled : await TryExecuteCompiledAsync(compiled, cancellationToken).ConfigureAwait(false); }
}

public sealed class CommandDispatcher
{
    private readonly IReadOnlyList<ICommandRoute> _routes; private readonly IExecutionResultCache _cache; private readonly IIdempotencyStore _idempotency; private readonly IVariableResolver? _variables;
    public CommandDispatcher(IEnumerable<ICommandRoute> routes) : this(routes, new InMemoryExecutionResultCache(), new InMemoryIdempotencyStore(), null) { }
    public CommandDispatcher(IEnumerable<ICommandRoute> routes, IExecutionResultCache cache) : this(routes, cache, new InMemoryIdempotencyStore(), null) { }
    public CommandDispatcher(IEnumerable<ICommandRoute> routes, IExecutionResultCache cache, IIdempotencyStore idempotency, IVariableResolver variables) : this(routes, cache, idempotency, (IVariableResolver?)variables) { }
    private CommandDispatcher(IEnumerable<ICommandRoute> routes, IExecutionResultCache cache, IIdempotencyStore idempotency, IVariableResolver? variables)
    { _routes = routes.ToArray(); _cache = cache; _idempotency = idempotency; _variables = variables; }

    public bool CanDispatch(BoundCommand command) => MatchingRoutes(command.Frame.Id).Any(route => route.CanHandle(command));
    public CompiledCommand? TryCompile(BoundCommand command)
    { foreach (ICommandRoute route in MatchingRoutes(command.Frame.Id)) { CompiledCommand? compiled = route.TryCompile(command); if (compiled is not null) return compiled; } return null; }

    public async ValueTask<CommandDispatchResult> TryExecuteAsync(CompiledCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? idempotencyKey = null;
        if (CommandExecutionArtifactStore.TryGetIdempotency(command.Source, out ExecutionIdempotencyPolicy? once))
        {
            if (_variables is null) throw new InvalidOperationException("Idempotent execution requires an IVariableResolver.");
            idempotencyKey = CommandExecutionArtifactStore.IdempotencyKey(command.Source, once!, _variables);
            if (_idempotency.TryGet(idempotencyKey, out object? recorded)) return CommandDispatchResult.Handled(recorded);
        }
        ExecutionCachePolicy? cachePolicy = null; string? cacheKey = null;
        if (CommandExecutionArtifactStore.TryGetCache(command.Source, out cachePolicy))
        {
            cacheKey = CommandExecutionArtifactStore.CommandFingerprint(command.Source);
            if (_cache.TryGet(cacheKey, out object? cached)) return CommandDispatchResult.Handled(cached);
        }
        foreach (ICommandRoute route in MatchingRoutes(command.FrameId))
        {
            CommandDispatchResult result = await route.TryExecuteCompiledAsync(command, cancellationToken).ConfigureAwait(false);
            if (!result.IsHandled) continue;
            if (cachePolicy is not null) _cache.Set(cacheKey!, result.Result, cachePolicy.Ttl);
            if (idempotencyKey is not null) _idempotency.Record(idempotencyKey, result.Result);
            return result;
        }
        return CommandDispatchResult.NotHandled;
    }

    public async ValueTask<CommandDispatchResult> TryExecuteAsync(BoundCommand command, CancellationToken cancellationToken = default)
    {
        CompiledCommand? compiled = TryCompile(command);
        if (compiled is not null) return await TryExecuteAsync(compiled, cancellationToken).ConfigureAwait(false);
        foreach (ICommandRoute route in MatchingRoutes(command.Frame.Id)) { CommandDispatchResult result = await route.TryExecuteAsync(command, cancellationToken).ConfigureAwait(false); if (result.IsHandled) return result; }
        return CommandDispatchResult.NotHandled;
    }
    private IEnumerable<ICommandRoute> MatchingRoutes(FrameId frameId)
    { ICommandRoute[] exact = _routes.Where(route => route.FrameId is { } id && id == frameId).ToArray(); return exact.Length > 0 ? exact : _routes.Where(route => route.FrameId is null); }
}

public static class TypedCommandServiceCollectionExtensions
{
    public static IServiceCollection AddTypedCommand<TCommand, TResult, TBinder, THandler>(this IServiceCollection services, FrameId frameId)
        where TCommand : class, ICommand<TResult> where TBinder : class, ICommandBinder<TCommand, TResult> where THandler : class, ICommandHandler<TCommand, TResult>
    { services.AddTransient<ICommandBinder<TCommand, TResult>, TBinder>(); services.AddTransient<ICommandHandler<TCommand, TResult>, THandler>(); services.AddTransient<ICommandRoute>(provider => new CommandRoute<TCommand, TResult>(frameId, provider.GetRequiredService<ICommandBinder<TCommand, TResult>>(), provider.GetRequiredService<ICommandHandler<TCommand, TResult>>())); return services; }
    public static IServiceCollection AddTypedCommand<TCommand, TResult, TBinder, THandler>(this IServiceCollection services)
        where TCommand : class, ICommand<TResult> where TBinder : class, ICommandBinder<TCommand, TResult> where THandler : class, ICommandHandler<TCommand, TResult>
    { services.AddTransient<ICommandBinder<TCommand, TResult>, TBinder>(); services.AddTransient<ICommandHandler<TCommand, TResult>, THandler>(); services.AddTransient<ICommandRoute>(provider => new CommandRoute<TCommand, TResult>(null, provider.GetRequiredService<ICommandBinder<TCommand, TResult>>(), provider.GetRequiredService<ICommandHandler<TCommand, TResult>>())); return services; }
}
