using FluNET.Execution.Commands;
using FluNET.Syntax.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace FluNET.Language;

public sealed record CommandRouteDescriptor(
    Type ImplementationType,
    Type CommandType,
    Type ResultType,
    Type BinderType,
    Type HandlerType);

internal sealed record PendingCommandRoute(
    CommandRouteDescriptor Descriptor,
    Action<IServiceCollection> Register);

/// <summary>
/// Collects language declarations and executable routes as one validated unit.
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
            services => services.AddTypedCommand<TCommand, TResult, TBinder, THandler>()));
        return this;
    }

    public FluNetRuntimeDefinition Build()
    {
        LanguageSnapshot language = Language.Build();
        Validate(language);
        return new FluNetRuntimeDefinition(language, _routes);
    }

    private void Validate(LanguageSnapshot language)
    {
        CommandFrameDescriptor[] frames = language.Commands
            .SelectMany(command => command.Frames)
            .ToArray();

        foreach (CommandFrameDescriptor frame in frames)
        {
            PendingCommandRoute[] matches = _routes
                .Where(route => route.Descriptor.ImplementationType == frame.ImplementationType)
                .ToArray();
            if (matches.Length != 1)
            {
                throw new LanguageDefinitionException(
                    $"Frame '{frame.ImplementationType.FullName}' must have exactly one typed route; " +
                    $"found {matches.Length}.");
            }
            if (matches[0].Descriptor.ResultType != frame.ResultType)
            {
                throw new LanguageDefinitionException(
                    $"Frame '{frame.ImplementationType.FullName}' declares result '{frame.ResultType}', " +
                    $"but its route returns '{matches[0].Descriptor.ResultType}'.");
            }
        }

        foreach (PendingCommandRoute route in _routes)
        {
            if (!frames.Any(frame => frame.ImplementationType == route.Descriptor.ImplementationType))
            {
                throw new LanguageDefinitionException(
                    $"Typed route '{route.Descriptor.CommandType.FullName}' has no language frame.");
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
        Language = language;
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
            route.Register(services);
        }
    }
}
