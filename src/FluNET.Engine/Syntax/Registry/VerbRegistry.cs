using FluNET.Syntax.Core;
using FluNET.Syntax.Nouns;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace FluNET.Syntax.Registry;

/// <summary>
/// Creates verbs registered by <see cref="LanguageRegistry"/>. The registry no
/// longer performs its own assembly scan.
/// </summary>
public sealed class VerbRegistry
{
    private readonly IServiceProvider _serviceProvider;
    private readonly LanguageRegistry _languageRegistry;
    private readonly Dictionary<Type, VerbMetadata> _metadata;

    private sealed record VerbMetadata(bool HasWhat, bool HasFrom, bool HasTo, bool HasUsing);

    public VerbRegistry(IServiceProvider serviceProvider, LanguageRegistry languageRegistry)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _languageRegistry = languageRegistry ?? throw new ArgumentNullException(nameof(languageRegistry));
        _metadata = languageRegistry.Verbs.ToDictionary(type => type, CreateMetadata);
    }

    public object CreateVerbInstance(Type verbType, params object?[] parameters)
    {
        if (!_metadata.ContainsKey(verbType))
        {
            throw new VerbNotFoundException($"Verb type '{verbType.FullName}' is not registered.");
        }

        ConstructorInfo[] constructors = verbType.GetConstructors()
            .Where(constructor => constructor.GetParameters().Length >= parameters.Length)
            .OrderByDescending(constructor => constructor.GetParameters().Length)
            .ToArray();

        foreach (ConstructorInfo constructor in constructors)
        {
            ParameterInfo[] constructorParameters = constructor.GetParameters();
            if (!ExplicitParametersMatch(constructorParameters, parameters))
            {
                continue;
            }

            object?[] arguments = new object?[constructorParameters.Length];
            Array.Copy(parameters, arguments, parameters.Length);

            bool canResolve = true;
            for (int index = parameters.Length; index < constructorParameters.Length; index++)
            {
                object? service = _serviceProvider.GetService(constructorParameters[index].ParameterType);
                if (service is null)
                {
                    canResolve = false;
                    break;
                }
                arguments[index] = service;
            }

            if (!canResolve)
            {
                continue;
            }

            try
            {
                return constructor.Invoke(arguments);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                throw new VerbActivationException(
                    $"Could not create verb '{verbType.FullName}': {exception.InnerException.Message}",
                    exception.InnerException);
            }
        }

        throw new VerbActivationException(
            $"No constructor on '{verbType.FullName}' accepts the resolved command arguments and registered capabilities.");
    }

    public (bool HasWhat, bool HasFrom, bool HasTo, bool HasUsing) GetVerbParameterInfo(Type verbType)
    {
        if (!_metadata.TryGetValue(verbType, out VerbMetadata? metadata))
        {
            throw new VerbNotFoundException($"Verb type '{verbType.FullName}' is not registered.");
        }

        return (metadata.HasWhat, metadata.HasFrom, metadata.HasTo, metadata.HasUsing);
    }

    public IVerb GetVerbByName(string name)
    {
        Type type = _languageRegistry.GetVerbType(name)
            ?? throw new VerbNotFoundException($"No verb found with name '{name}'.");

        return ActivatorUtilities.CreateInstance(_serviceProvider, type) as IVerb
            ?? throw new VerbActivationException($"Could not create verb '{type.FullName}'.");
    }

    public IEnumerable<string> GetAllVerbNames() => _languageRegistry.VerbNames;

    public int Count => _languageRegistry.VerbNames.Count();

    private static VerbMetadata CreateMetadata(Type verbType)
    {
        Type[] interfaces = verbType.GetInterfaces();
        return new VerbMetadata(
            HasGenericInterface(interfaces, typeof(IWhat<>)),
            HasGenericInterface(interfaces, typeof(IFrom<>)),
            HasGenericInterface(interfaces, typeof(ITo<>)),
            HasGenericInterface(interfaces, typeof(IUsing<>)));
    }

    private static bool HasGenericInterface(IEnumerable<Type> interfaces, Type genericDefinition) =>
        interfaces.Any(type => type.IsGenericType && type.GetGenericTypeDefinition() == genericDefinition);

    private static bool ExplicitParametersMatch(
        IReadOnlyList<ParameterInfo> constructorParameters,
        IReadOnlyList<object?> values)
    {
        for (int index = 0; index < values.Count; index++)
        {
            object? value = values[index];
            Type targetType = constructorParameters[index].ParameterType;
            if (value is null)
            {
                if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) is null)
                {
                    return false;
                }
            }
            else if (!targetType.IsInstanceOfType(value))
            {
                return false;
            }
        }

        return true;
    }
}

public sealed class VerbNotFoundException : Exception
{
    public VerbNotFoundException(string message) : base(message)
    {
    }
}

public sealed class VerbActivationException : Exception
{
    public VerbActivationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
