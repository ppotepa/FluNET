using FluNET.Language.Metadata;
using FluNET.Syntax.Ast;
using FluNET.Syntax.Core;

namespace FluNET.Binding;

public sealed record ActivationContext(IReadOnlyDictionary<string, object?>? Variables = null, object? PipelineValue = null, IServiceProvider? Services = null);

public sealed class VerbActivator
{
    public IVerb Create(BoundSentence sentence, ActivationContext? context = null)
    {
        context ??= new ActivationContext(); ConstructorDescriptor? constructor = sentence.Constructor;
        if (constructor == null) { IVerb? fallback = sentence.Verb.Factory(); return fallback ?? throw new InvalidOperationException($"Verb '{sentence.Verb.VerbType.FullName}' has no usable constructor or factory."); }
        var remainingRoles = sentence.Roles.ToList(); object?[] arguments = new object?[constructor.Parameters.Count];
        for (int i = 0; i < constructor.Parameters.Count; i++)
        {
            ParameterDescriptor p = constructor.Parameters[i];
            if (p.FromServices || p.Role == null)
            {
                object? service = context.Services?.GetService(p.ParameterType); if (service != null) { arguments[i] = service; continue; }
                if (p.Role == null) { if (p.Parameter.HasDefaultValue) { arguments[i] = p.Parameter.DefaultValue; continue; } if (p.IsOptional) { arguments[i] = DefaultValue(p.ParameterType); continue; } throw new InvalidOperationException($"Cannot resolve service parameter '{p.Name}' ({p.ParameterType.Name}) for '{sentence.Verb.Text}'."); }
            }
            BoundRole? role = FindRole(p, remainingRoles); if (role == null) { if (p.IsOptional) { arguments[i] = p.Parameter.HasDefaultValue ? p.Parameter.DefaultValue : DefaultValue(p.ParameterType); continue; } throw new InvalidOperationException($"Missing bound role '{p.Role}' for constructor parameter '{p.Name}'."); }
            remainingRoles.Remove(role); arguments[i] = MaterializeRole(p, role, context);
        }
        object instance = constructor.Activator(arguments);
        return instance as IVerb ?? throw new InvalidOperationException($"Constructed type '{instance.GetType().FullName}' is not an IVerb.");
    }

    private static BoundRole? FindRole(ParameterDescriptor p, IReadOnlyList<BoundRole> roles) { BoundRole? named = roles.FirstOrDefault(x => x.Descriptor.Kind == p.Role && !string.IsNullOrWhiteSpace(x.Descriptor.Name) && x.Descriptor.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase)); return named ?? roles.FirstOrDefault(x => x.Descriptor.Kind == p.Role); }
    private static object? MaterializeRole(ParameterDescriptor p, BoundRole r, ActivationContext c) { if (r.Values.Count == 0) return DefaultValue(p.ParameterType); if (r.Values.Count == 1) return MaterializeValue(r.Values[0], p.ParameterType, r.Descriptor.Direction, c); if (p.ParameterType.IsArray) { Type e = p.ParameterType.GetElementType()!; Array a = Array.CreateInstance(e, r.Values.Count); for (int i = 0; i < r.Values.Count; i++) a.SetValue(MaterializeValue(r.Values[i], e, r.Descriptor.Direction, c), i); return a; } throw new InvalidOperationException($"Role '{r.Descriptor.Kind}' produced multiple values for non-collection parameter '{p.Name}'."); }
    private static object? MaterializeValue(BoundValue v, Type t, RoleDirection d, ActivationContext c) { object? raw; if (v.ConstantValue != null) raw = v.ConstantValue; else raw = v.Source switch { VariableExpression variable when d == RoleDirection.Output => DefaultValue(t), VariableExpression variable when c.Variables != null && c.Variables.TryGetValue(variable.Name, out object? vv) => vv, VariableExpression variable => throw new InvalidOperationException($"Variable '{variable.Name}' has no runtime value."), PipelineValueExpression => c.PipelineValue, InterpolatedStringExpression s when t == typeof(string) => s.Template, _ => DefaultValue(t) }; return v.Conversion?.Apply(raw) ?? raw; }
    private static object? DefaultValue(Type t) { if (t.IsArray) return Array.CreateInstance(t.GetElementType()!, 0); if (t.IsValueType) return Activator.CreateInstance(t); return null; }
}
