using FluNET.Syntax.Ast;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;

namespace FluNET.Binding;

/// <summary>
/// Evaluates bound expression sources against runtime variables/pipeline values.
/// Reflection accessors are cached per CLR type/property.
/// </summary>
public sealed class ExpressionRuntimeEvaluator
{
    private static readonly Regex Interpolation = new(@"\[(?<path>[A-Za-z_][A-Za-z0-9_\.]*)\]", RegexOptions.Compiled);
    private readonly ConcurrentDictionary<(Type Type, string Property), PropertyInfo?> _properties = new();

    public object? Evaluate(ExpressionNode expression, ActivationContext context)
    {
        return expression switch
        {
            VariableExpression variable => ResolveVariable(variable.Name, context),
            PropertyExpression property => ResolveProperty(property, context),
            PipelineValueExpression => context.PipelineValue,
            InterpolatedStringExpression interpolated => Interpolate(interpolated.Template, context),
            LiteralExpression literal => literal.Value,
            ReferenceExpression reference => reference.Reference,
            _ => null
        };
    }

    private object? ResolveVariable(string name, ActivationContext context)
    {
        if (context.Variables != null && context.Variables.TryGetValue(name, out object? value))
            return value;
        throw new InvalidOperationException($"Variable '{name}' has no runtime value.");
    }

    private object? ResolveProperty(PropertyExpression property, ActivationContext context)
    {
        object? target = Evaluate(property.Target, context);
        if (target == null) return null;

        PropertyInfo? info = _properties.GetOrAdd((target.GetType(), property.Property), key =>
            key.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(x => x.Name.Equals(key.Property, StringComparison.OrdinalIgnoreCase)));

        if (info == null)
            throw new InvalidOperationException($"Property '{property.Property}' does not exist on '{target.GetType().FullName}'.");

        return info.GetValue(target);
    }

    private string Interpolate(string template, ActivationContext context) =>
        Interpolation.Replace(template, match =>
        {
            ExpressionNode expression = ParsePath(match.Groups["path"].Value);
            return Evaluate(expression, context)?.ToString() ?? string.Empty;
        });

    private static ExpressionNode ParsePath(string path)
    {
        string[] parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        ExpressionNode current = new VariableExpression(parts[0]);
        for (int i = 1; i < parts.Length; i++) current = new PropertyExpression(current, parts[i]);
        return current;
    }
}
