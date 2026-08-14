using FluNET.Compilation;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Telemetry;
using FluNET.Variables;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

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
{
    public static CommandDispatchResult NotHandled => new(false, null);
    public static CommandDispatchResult Handled(object? result) => new(true, result);
}

public sealed class CommandRoute<TCommand, TResult> : ICommandRoute where TCommand : class, ICommand<TResult>
{
    private readonly ICommandBinder<TCommand, TResult> binder;
    private readonly ICommandHandler<TCommand, TResult> handler;

    public CommandRoute(ICommandBinder<TCommand, TResult> binder, ICommandHandler<TCommand, TResult> handler)
        : this(null, binder, handler) { }

    public CommandRoute(FrameId? frameId, ICommandBinder<TCommand, TResult> binder, ICommandHandler<TCommand, TResult> handler)
    {
        FrameId = frameId;
        this.binder = binder;
        this.handler = handler;
    }

    public FrameId? FrameId { get; }
    public bool CanHandle(BoundCommand command) =>
        (FrameId is null || FrameId == command.Frame.Id) && binder.TryBind(command) is not null;

    public CompiledCommand? TryCompile(BoundCommand command)
    {
        if (FrameId is { } id && command.Frame.Id != id) return null;
        TCommand? typed = binder.TryBind(command);
        return typed is null
            ? null
            : new CompiledCommand(command, typed, typeof(TCommand), typeof(TResult), command.Frame.ResultTypeSymbol);
    }

    public async ValueTask<CommandDispatchResult> TryExecuteCompiledAsync(
        CompiledCommand command,
        CancellationToken cancellationToken = default)
    {
        if (FrameId is { } id && command.FrameId != id || command.Value is not TCommand typed)
            return CommandDispatchResult.NotHandled;
        return CommandDispatchResult.Handled(
            await handler.HandleAsync(typed, cancellationToken).ConfigureAwait(false));
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

public sealed class CommandDispatcher
{
    private static readonly IExecutionMetadataProvider Metadata = new DefaultExecutionMetadataProvider();
    private readonly IReadOnlyList<ICommandRoute> routes;
    private readonly IExecutionResultCache cache;
    private readonly IIdempotencyStore idempotency;
    private readonly IVariableResolver? variables;
    private readonly IFluNetTelemetrySink telemetry;

    public CommandDispatcher(IEnumerable<ICommandRoute> routes)
        : this(routes, new InMemoryExecutionResultCache(), new InMemoryIdempotencyStore(), null, NullFluNetTelemetrySink.Instance) { }

    public CommandDispatcher(IEnumerable<ICommandRoute> routes, IExecutionResultCache cache)
        : this(routes, cache, new InMemoryIdempotencyStore(), null, NullFluNetTelemetrySink.Instance) { }

    public CommandDispatcher(
        IEnumerable<ICommandRoute> routes,
        IExecutionResultCache cache,
        IIdempotencyStore idempotency,
        IVariableResolver variables)
        : this(routes, cache, idempotency, variables, NullFluNetTelemetrySink.Instance) { }

    public CommandDispatcher(
        IEnumerable<ICommandRoute> routes,
        IExecutionResultCache cache,
        IIdempotencyStore idempotency,
        IVariableResolver? variables,
        IFluNetTelemetrySink telemetry)
    {
        this.routes = routes?.ToArray() ?? throw new ArgumentNullException(nameof(routes));
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        this.idempotency = idempotency ?? throw new ArgumentNullException(nameof(idempotency));
        this.variables = variables;
        this.telemetry = telemetry ?? NullFluNetTelemetrySink.Instance;
    }

    public bool CanDispatch(BoundCommand command) => MatchingRoutes(command.Frame.Id).Any(route => route.CanHandle(command));

    public CompiledCommand? TryCompile(BoundCommand command)
    {
        foreach (ICommandRoute route in MatchingRoutes(command.Frame.Id))
        {
            CompiledCommand? compiled = route.TryCompile(command);
            if (compiled is not null) return compiled;
        }
        return null;
    }

    public async ValueTask<CommandDispatchResult> TryExecuteAsync(
        CompiledCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        long started = Stopwatch.GetTimestamp();
        string? idempotencyKey = null;
        bool hasIdempotency = CommandExecutionArtifactStore.TryGetIdempotency(
            command.Source,
            out ExecutionIdempotencyPolicy? once);
        bool hasCache = CommandExecutionArtifactStore.TryGetCache(
            command.Source,
            out ExecutionCachePolicy? cachePolicy);

        try
        {
            if (hasIdempotency)
            {
                if (variables is null)
                    throw new InvalidOperationException("Idempotent execution requires an IVariableResolver.");
                idempotencyKey = CommandExecutionArtifactStore.IdempotencyKey(command.Source, once!, variables);
                if (idempotency.TryGet(idempotencyKey, out object? recorded))
                {
                    await EmitAsync(command, "idempotency-hit", started, hasCache, hasIdempotency).ConfigureAwait(false);
                    return CommandDispatchResult.Handled(recorded);
                }
            }

            string? cacheKey = null;
            if (hasCache)
            {
                cacheKey = CommandExecutionArtifactStore.CommandFingerprint(command.Source);
                if (cache.TryGet(cacheKey, out object? cached))
                {
                    await EmitAsync(command, "cache-hit", started, hasCache, hasIdempotency).ConfigureAwait(false);
                    return CommandDispatchResult.Handled(cached);
                }
            }

            foreach (ICommandRoute route in MatchingRoutes(command.FrameId))
            {
                CommandDispatchResult result = await route
                    .TryExecuteCompiledAsync(command, cancellationToken)
                    .ConfigureAwait(false);
                if (!result.IsHandled) continue;
                if (hasCache) cache.Set(cacheKey!, result.Result, cachePolicy!.Ttl);
                if (idempotencyKey is not null) idempotency.Record(idempotencyKey, result.Result);
                await EmitAsync(command, "succeeded", started, hasCache, hasIdempotency).ConfigureAwait(false);
                return result;
            }

            await EmitAsync(command, "not-handled", started, hasCache, hasIdempotency).ConfigureAwait(false);
            return CommandDispatchResult.NotHandled;
        }
        catch
        {
            await EmitAsync(command, "failed", started, hasCache, hasIdempotency).ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask<CommandDispatchResult> TryExecuteAsync(
        BoundCommand command,
        CancellationToken cancellationToken = default)
    {
        CompiledCommand? compiled = TryCompile(command);
        if (compiled is not null)
            return await TryExecuteAsync(compiled, cancellationToken).ConfigureAwait(false);

        foreach (ICommandRoute route in MatchingRoutes(command.Frame.Id))
        {
            CommandDispatchResult result = await route.TryExecuteAsync(command, cancellationToken).ConfigureAwait(false);
            if (result.IsHandled) return result;
        }
        return CommandDispatchResult.NotHandled;
    }

    private ValueTask EmitAsync(
        CompiledCommand command,
        string outcome,
        long started,
        bool hasCache,
        bool hasIdempotency)
    {
        FrameExecutionMetadata metadata = Metadata.Get(command.Source.Frame);
        Dictionary<string, string> attributes = new(StringComparer.Ordinal)
        {
            ["frame.id"] = command.FrameId.Value,
            ["effect"] = metadata.Effect.ToString(),
            ["concurrency"] = metadata.Concurrency.ToString(),
            ["result.type"] = command.ResultTypeSymbol.Id.Value,
            ["cache.enabled"] = hasCache.ToString(),
            ["idempotency.enabled"] = hasIdempotency.ToString()
        };
        return FluNetTelemetry.TryEmitAsync(telemetry, new(
            DateTimeOffset.UtcNow,
            "command",
            "dispatch",
            outcome,
            Stopwatch.GetElapsedTime(started),
            attributes));
    }

    private IEnumerable<ICommandRoute> MatchingRoutes(FrameId frameId)
    {
        ICommandRoute[] exact = routes
            .Where(route => route.FrameId is { } id && id == frameId)
            .ToArray();
        return exact.Length > 0 ? exact : routes.Where(route => route.FrameId is null);
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
        services.AddTransient<ICommandBinder<TCommand, TResult>, TBinder>();
        services.AddTransient<ICommandHandler<TCommand, TResult>, THandler>();
        services.AddTransient<ICommandRoute>(provider => new CommandRoute<TCommand, TResult>(
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
        services.AddTransient<ICommandBinder<TCommand, TResult>, TBinder>();
        services.AddTransient<ICommandHandler<TCommand, TResult>, THandler>();
        services.AddTransient<ICommandRoute>(provider => new CommandRoute<TCommand, TResult>(
            null,
            provider.GetRequiredService<ICommandBinder<TCommand, TResult>>(),
            provider.GetRequiredService<ICommandHandler<TCommand, TResult>>()));
        return services;
    }
}
