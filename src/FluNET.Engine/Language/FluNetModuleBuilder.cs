using FluNET.Execution.Commands;
using FluNET.Language.Values;
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

    public CommandRouteDescriptor(
        Type implementationType,
        Type commandType,
        Type resultType,
        Type binderType,
        Type handlerType)
        : this(default(FrameId), commandType, resultType, binderType, handlerType)
    {
        ImplementationType = implementationType ?? throw new ArgumentNullException(nameof(implementationType));
    }

    public FrameId FrameId { get; internal set; }
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
/// Collects language declarations, value-system extensions, and executable routes
/// as one atomically validated runtime definition.
/// </summary>
public sealed class FluNetModuleBuilder
{
    private readonly List<PendingCommandRoute> _routes = [];
    private readonly List<ValueCodecRegistration> _codecs = [];
    private readonly List<ValueConversionRegistration> _conversions = [];

    public LanguageBuilder Language { get; } = new();

    public FluNetModuleBuilder AddModule(IFluNetModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        module.Register(this);
        return this;
    }

    public FluNetModuleBuilder Codec<TValue, TCodec>()
        where TCodec : class, FluNET.Language.Values.IValueCodec<TValue>
    {
        _codecs.Add(new ValueCodecRegistration(
            typeof(TValue),
            typeof(TCodec),
            (services, typeId) => new RuntimeValueCodec<TValue>(
                typeId,
                ActivatorUtilities.CreateInstance<TCodec>(services))));
        return this;
    }

    public FluNetModuleBuilder Conversion<TSource, TTarget, TConversion>(
        ConversionKind kind = ConversionKind.Implicit,
        int cost = 1)
        where TConversion : class, IValueConversion<TSource, TTarget>
    {
        if (cost <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cost),
                "Conversion cost must be positive.");
        }

        _conversions.Add(new ValueConversionRegistration(
            typeof(TSource),
            typeof(TTarget),
            typeof(TConversion),
            kind,
            cost,
            (services, descriptor) => new RuntimeValueConversion<TSource, TTarget>(
                descriptor,
                ActivatorUtilities.CreateInstance<TConversion>(services))));
        return this;
    }

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

    public FluNetModuleBuilder Route<TCommand, TResult, TBinder, THandler>(string frameId)
        where TCommand : class, ICommand<TResult>
        where TBinder : class, ICommandBinder<TCommand, TResult>
        where THandler : class, ICommandHandler<TCommand, TResult> =>
        Route<TCommand, TResult, TBinder, THandler>(new FrameId(frameId));

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
        ValidateRoutes(language);
        ValidateValues(language);
        return new FluNetRuntimeDefinition(language, _routes, _codecs, _conversions);
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
                .Where(frame =>
                    frame.HasLegacyVerbAdapter &&
                    frame.ImplementationType == implementationType)
                .ToArray();
            if (matches.Length != 1)
            {
                throw new LanguageDefinitionException(
                    $"Legacy route implementation '{implementationType?.FullName}' must resolve " +
                    $"to exactly one frame; found {matches.Length}.");
            }
            route.Descriptor.FrameId = matches[0].Id;
        }
    }

    private void ValidateRoutes(LanguageSnapshot language)
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

    private void ValidateValues(LanguageSnapshot language)
    {
        HashSet<TypeId> codecTypes = [];
        foreach (ValueCodecRegistration registration in _codecs)
        {
            TypeSymbol type = language.Types.Get(registration.ValueType);
            if (!codecTypes.Add(type.Id))
            {
                throw new LanguageDefinitionException(
                    $"A custom value codec for '{type.Id}' is registered more than once.");
            }
        }

        foreach (ValueConversionRegistration registration in _conversions)
        {
            TypeSymbol source = language.Types.Get(registration.SourceType);
            TypeSymbol target = language.Types.Get(registration.TargetType);
            if (source.Id == target.Id)
            {
                throw new LanguageDefinitionException(
                    $"Conversion '{registration.ConversionType.FullName}' maps '{source.Id}' to itself.");
            }
        }
    }
}

/// <summary>An immutable language plus registrations needed to compile and execute it.</summary>
public sealed class FluNetRuntimeDefinition
{
    private readonly ReadOnlyCollection<PendingCommandRoute> _routes;
    private readonly ReadOnlyCollection<ValueCodecRegistration> _codecs;
    private readonly ReadOnlyCollection<ValueConversionRegistration> _conversions;

    internal FluNetRuntimeDefinition(
        LanguageSnapshot language,
        IEnumerable<PendingCommandRoute> routes,
        IEnumerable<ValueCodecRegistration>? codecs = null,
        IEnumerable<ValueConversionRegistration>? conversions = null)
    {
        Language = language ?? throw new ArgumentNullException(nameof(language));
        _routes = Array.AsReadOnly(routes.ToArray());
        _codecs = Array.AsReadOnly(codecs?.ToArray() ?? []);
        _conversions = Array.AsReadOnly(conversions?.ToArray() ?? []);
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
        services.AddSingleton<IValueCodecRegistry>(provider =>
            new ValueCodecRegistry(Language, provider, _codecs, _conversions));
    }
}
