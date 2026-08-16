using FluNET.Language.Metadata;
using FluNET.Syntax.Ast;
using FluNET.Syntax.Core;

namespace FluNET.Binding;

public sealed record ActivationContext(
    IReadOnlyDictionary<string, object?>? Variables = null,
    object? PipelineValue = null,
    IServiceProvider? Services = null);

/// <summary>
/// Materializes a bound verb through the constructor selected from reflection metadata.
/// Language-role parameters come from bound values; non-role parameters may come from DI.
/// </summary>
public sealed class VerbActivator
{
    public IVerb Create(BoundSentence sentence, ActivationContext? context = null)
    {
        context ??= new ActivationContext();
        ConstructorDescriptor? constructor = sentence.Constructor;

        if (constructor == null)
        {
            IVerb? fallback = sentence.Verb.Factory();
            return fallback ?? throw new InvalidOperationException(
                $"Verb '{sentence.Verb.VerbType.FullName}' has no usable constructor or factory.");
        }

        var remainingRoles = sentence.Roles.ToList();
        object?[] arguments = new object?[constructor.Parameters.Count];

        for (int i = 0; i < constructor.Parameters.Count; i++)
        {
            ParameterDescriptor parameter = constructor.Parameters[i];

            if (parameter.FromServices || parameter.Role == null)
            {
                object? service = context.Services?.GetService(parameter.ParameterType);
                if (service != null)
                {
                    arguments[i] = service;
                    continue;
                }

                if (parameter.Role == null)
                {
                    if (parameter.Parameter.HasDefaultValue)
                    {
                        arguments[i] = parameter.Parameter.DefaultValue;
                        continue;
                    }

                    if (parameter.IsOptional)
                    {
                        arguments[i] = DefaultValue(parameter.ParameterType);
                        continue;
                    }

                    throw new InvalidOperationException(
                        $"Cannot resolve service parameter '{parameter.Name}' ({parameter.ParameterType.Name}) for '{sentence.Verb.Text}'.");
                }
            }

            BoundRole? role = FindRole(parameter, remainingRoles);
            if (role == null)
            {
                if (parameter.IsOptional)
                {
                    arguments[i] = parameter.Parameter.HasDefaultValue
                        ? parameter.Parameter.DefaultValue
                        : DefaultValue(parameter.ParameterType);
                    continue;
                }

                throw new InvalidOperationException(
                    $"Missing bound role '{parameter.Role}' for constructor parameter '{parameter.Name}'.");
            }

            remainingRoles.Remove(role);
            arguments[i] = MaterializeRole(parameter, role, context);
        }

        object instance = constructor.Constructor.Invoke(arguments);
        return instance as IVerb ?? throw new InvalidOperationException(
            $"Constructed type '{instance.GetType().FullName}' is not an IVerb.");
    }

    private static BoundRole? FindRole(ParameterDescriptor parameter, IReadOnlyList<BoundRole> roles)
    {
        BoundRole? named = roles.FirstOrDefault(x =>
            x.Descriptor.Kind == parameter.Role
            && !string.IsNullOrWhiteSpace(x.Descriptor.Name)
            && x.Descriptor.Name.Equals(parameter.Name, StringComparison.OrdinalIgnoreCase));

        return named ?? roles.FirstOrDefault(x => x.Descriptor.Kind == parameter.Role);
    }

    private static object? MaterializeRole(ParameterDescriptor parameter, BoundRole role, ActivationContext context)
    {
        if (role.Values.Count == 0)
            return DefaultValue(parameter.ParameterType);

        if (role.Values.Count == 1)
            return MaterializeValue(role.Values[0], parameter.ParameterType, role.Descriptor.Direction, context);

        if (parameter.ParameterType.IsArray)
        {
            Type elementType = parameter.ParameterType.GetElementType()!;
            Array array = Array.CreateInstance(elementType, role.Values.Count);
            for (int i = 0; i < role.Values.Count; i++)
                array.SetValue(MaterializeValue(role.Values[i], elementType, role.Descriptor.Direction, context), i);
            return array;
        }

        throw new InvalidOperationException(
            $"Role '{role.Descriptor.Kind}' produced multiple values for non-collection parameter '{parameter.Name}'.");
    }

    private static object? MaterializeValue(
        BoundValue value,
        Type targetType,
        RoleDirection direction,
        ActivationContext context)
    {
        if (value.ConstantValue != null)
            return value.ConstantValue;

        switch (value.Source)
        {
            case VariableExpression variable when direction == RoleDirection.Output:
                return DefaultValue(targetType);

            case VariableExpression variable:
                if (context.Variables != null && context.Variables.TryGetValue(variable.Name, out object? variableValue))
                    return variableValue;
                throw new InvalidOperationException($"Variable '{variable.Name}' has no runtime value.");

            case PipelineValueExpression:
                return context.PipelineValue;

            case InterpolatedStringExpression interpolated when targetType == typeof(string):
                return interpolated.Template;
        }

        return DefaultValue(targetType);
    }

    private static object? DefaultValue(Type type)
    {
        if (type.IsArray)
            return Array.CreateInstance(type.GetElementType()!, 0);

        if (type.IsValueType)
            return Activator.CreateInstance(type);

        return null;
    }
}
