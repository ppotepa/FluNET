using FluNET.Execution.Commands;
using FluNET.Syntax.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace FluNET.Language;

/// <summary>Executable route metadata keyed by stable semantic frame identity.</summary>
public sealed record CommandRouteDescriptor
{
    public CommandRouteDescriptor(
        FrameId frameId,
        Type commandType,
        Type resultType,
        Type binderType,
        Type handlerType)
    {
        FrameId = frameId;
        CommandType = commandType ?? throw new ArgumentNullException(nameof(commandType));
        ResultType = resultType ?? throw new ArgumentNullException(nameof(resultType));
        BinderType = binderType ?? throw new ArgumentNullException(nameof(binderType));
        HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType));
    }

    /// <summary>Compatibility constructor for routes declared through a legacy verb adapter.</summary>
    public CommandRouteDescriptor(
        Type implementationType,
        Type commandType,
        Type resultType,
        Type binderType,
        Type handlerType)
        : this(default, commandType, resultType, binderType, handlerType)
    {
        ImplementationType = implementationType ?? throw new ArgumentNullException(nameof(implementationType));
    }

    public FrameId FrameId { get; internal set; }

    /// <summary>Legacy adapter type used only to resolve a FrameId during Build.</summary>
    public Type? ImplementationType { get; }

    public Type CommandType { get; }
    public Type ResultType { get; }
    public Type BinderType { get; }
    public Type HandlerType { get; }
}

internal sealed record PendingCommandRoute(
    CommandRouteDescriptor Descriptor,
    Action<IServiceCollection, FrameId> Register);

/// <summary>
/// Collects language declarations and executable routes as one validated unit.
/// Canonical routes are identified by FrameId rather than CLR verb types.
/// </summary>
public sealed class FluNetModuleBuilder
{
    private readonly List<PendingCommandRoute> _routes = [];

    public LanguageBuilder Language { get; } = new();

    public FluNetModuleBuilder AddModule(IFluNetModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        module.Register(this);
        return this;
    }

    /// <summary>Registers a typed route directly against a stable frame id.</summary>
    public FluNetModuleBuilder Route<TCommand, TResult, TBinder, THandler>(FrameId frameId)
        where TCommand : class, ICommand<TResult>
        where TBinder : class, ICommandBinder<TCommand, TResult>
        where THandler : class, ICommandHandler<TCommand, TResult>
    {
        if (frameId.IsEmpty)
        {
            throw new ArgumentException("A frame id is required.", nameof(frameId));
        }

        CommandRouteDescriptor descriptor = new(
            frameId,
            typeof(TCommand),
            typeof(TResult),
            typeof(TBinder),
            typeof(THandler));
        _routes.Add(new PendingCommandRoute(
            descriptor,
            (services, resolvedFrameId) =>
                services.AddTypedCommand<TCommand, TResult, TBinder, THandler>(resolvedFrameId)));
        return this;
    }

    /// <summary>Registers a typed route directly against a stable frame id.</summary>
    public FluNetModuleBuilder Route<TCommand, TResult, TBinder, THandler>(string frameId)
        where TCommand : class, ICommand<TResult>
        where TBinder : class, ICommandBinder<TCommand, TResult>
        where THandler : class, ICommandHandler<TCommand, TResult> =>
        Route<TCommand, TResult, TBinder, THandler>(new FrameId(frameId));

    /// <summary>
    /// Compatibility route declaration. The implementation type is resolved to
    /// exactly one frame id during Build and is not used by runtime dispatch.
    /// </summary>
    public FluNetModuleBuilder Route<TImplementation, TCommand, TResult, TBinder, THandler>()
        where TImplementation : class, IVerb
        where TCommand : class, ICommand<TResult>
        where TBinder : class, ICommandBinder<TCommand, TResult>
        where THandler : class, ICommandHandler<TCommand, TResult>
    {
        CommandRouteDescriptor descriptor = new(
            typeof(TImplementation),
            typeof(TCommand),
            typeof(TResult),
            typeof(TBinder),
            typeof(THandler));
        _routes.Add(new PendingCommandRoute(
            descriptor,
            (services, resolvedFrameId) =>
                services.AddTypedCommand<TCommand, TResult, TBinder, THandler>(resolvedFrameId)));
        return this;
    }

    public FluNetRuntimeDefinition Build()
    {
        LanguageSnapshot language = Language.Build();
        ResolveLegacyFrameIds(language);
        Validate(language);
        return new FluNetRuntimeDefinition(language, _routes);
    }

    private void ResolveLegacyFrameIds(LanguageSnapshot language)
    {
        CommandFrameDescriptor[] frames = language.Commands
            .SelectMany(command => command.Frames)
            .ToArray();

        foreach (PendingCommandRoute route in _routes.Where(route => route.Descriptor.FrameId.IsEmpty))
        {
            Type? implementationType = route.Descriptor.ImplementationType;
            CommandFrameDescriptor[] matches = frames
                .Where(frame => frame.ImplementationType == implementationType)
                .ToArray();
            if (matches.Length != 1)
            {
                throw new LanguageDefinitionException(
                    $"Legacy route implementation '{implementationType?.FullName}' must resolve to exactly one frame; " +
                    $"found {matches.Length}.");
            }

            route.Descriptor.FrameId = matches[0].Id;
        }
    }

    private void Validate(LanguageSnapshot language)
    {
        CommandFrameDescriptor[] frames = language.Commands
            .SelectMany(command => command.Frames)
            .ToArray();

        foreach (CommandFrameDescriptor frame in frames)
        {
            PendingCommandRoute[] matches = _routes
                .Where(route => route.Descriptor.FrameId == frame.Id)
                .ToArray();
            if (matches.Length != 1)
            {
                throw new LanguageDefinitionException(
                    $"Frame '{frame.Id}' must have exactly one typed route; found {matches.Length}.");
            }
            if (matches[0].Descriptor.ResultType != frame.ResultType)
            {
                throw new LanguageDefinitionException(
                    $"Frame '{frame.Id}' declares result '{frame.ResultType}', " +
                    $"but its route returns '{matches[0].Descriptor.ResultType}'.");
            }
        }

        foreach (PendingCommandRoute route in _routes)
        {
            if (!frames.Any(frame => frame.Id == route.Descriptor.FrameId))
            {
                throw new LanguageDefinitionException(
                    $"Typed route '{route.Descriptor.CommandType.FullName}' targets unknown frame " +
                    $"'{route.Descriptor.FrameId}'.");
            }
        }
    }
}

/// <summary>An immutable language plus all DI registrations needed to execute it.</summary>
public sealed class FluNetRuntimeDefinition
{
    private readonly ReadOnlyCollection<PendingCommandRoute> _routes;

    internal FluNetRuntimeDefinition(
        LanguageSnapshot language,
        IEnumerable<PendingCommandRoute> routes)
    {
        Language = language ?? throw new ArgumentNullException(nameof(language));
        _routes = Array.AsReadOnly(routes.ToArray());
        Routes = Array.AsReadOnly(_routes.Select(route => route.Descriptor).ToArray());
    }

    public LanguageSnapshot Language { get; }
    public IReadOnlyList<CommandRouteDescriptor> Routes { get; }

    public void RegisterRoutes(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        foreach (PendingCommandRoute route in _routes)
        {
            route.Register(services, route.Descriptor.FrameId);
        }
    }
}
