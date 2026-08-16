using FluNET.Variables;
using System.Collections.ObjectModel;

namespace FluNET.Execution.Commands;

/// <summary>Read-only services available while evaluating a command value.</summary>
public interface IExpressionEvaluationContext
{
    IVariableResolver Variables { get; }
}

public sealed class ExpressionEvaluationContext(IVariableResolver variables)
    : IExpressionEvaluationContext
{
    public IVariableResolver Variables { get; } =
        variables ?? throw new ArgumentNullException(nameof(variables));
}

/// <summary>The common typed value tree consumed by every command.</summary>
public interface IExpression<out TValue>
{
    Type ResultType => typeof(TValue);
    TValue Evaluate(IExpressionEvaluationContext context);
}

/// <summary>Typed expression interface used by command value trees.</summary>
public interface IValueExpression<out TValue> : IExpression<TValue>
{
}

public static class ExpressionEvaluationExtensions
{
    public static TValue Evaluate<TValue>(
        this IExpression<TValue> expression,
        IVariableResolver variables)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return expression.Evaluate(new ExpressionEvaluationContext(variables));
    }
}

public sealed record LiteralExpression<TValue>(TValue Value) : IExpression<TValue>
{
    public TValue Evaluate(IExpressionEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Value;
    }
}

public sealed class VariableExpression<TValue>(string reference, IValueCodec<TValue>? codec = null)
    : IExpression<TValue>
{
    public string Reference { get; } = string.IsNullOrWhiteSpace(reference)
        ? throw new ArgumentException("A variable reference is required.", nameof(reference))
        : reference.TrimEnd('.');

    public TValue Evaluate(IExpressionEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        object value = context.Variables.Resolve<object>(Reference)
            ?? throw new InvalidOperationException($"Variable {Reference} not found in context.");
        if (value is TValue typed)
        {
            return typed;
        }
        if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(typeof(TValue)))
        {
            try
            {
                return (TValue)Convert.ChangeType(
                    value,
                    typeof(TValue),
                    System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception exception) when (
                exception is InvalidCastException or FormatException or OverflowException)
            {
                // Preserve the existing diagnostic below when conversion fails.
            }
        }
        return codec is not null
            ? codec.Decode(value)
            : throw new InvalidCastException(
                $"Variable {Reference} contains '{value.GetType()}', expected '{typeof(TValue)}'.");
    }
}

public sealed class ListExpression<TValue> : IExpression<IReadOnlyList<TValue>>
{
    private readonly ReadOnlyCollection<IExpression<TValue>> _items;

    public ListExpression(IEnumerable<IExpression<TValue>> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = Array.AsReadOnly(items.ToArray());
    }

    public IReadOnlyList<IExpression<TValue>> Items => _items;

    public IReadOnlyList<TValue> Evaluate(IExpressionEvaluationContext context) =>
        _items.Select(item => item.Evaluate(context)).ToArray();
}

public sealed record ConversionExpression<TSource, TResult>(
    IExpression<TSource> Source,
    Func<TSource, TResult> Convert) : IExpression<TResult>
{
    public TResult Evaluate(IExpressionEvaluationContext context) =>
        Convert(Source.Evaluate(context));
}

public sealed record PropertyExpression<TObject, TValue>(
    IExpression<TObject> Source,
    Func<TObject, TValue> Select) : IExpression<TValue>
{
    public TValue Evaluate(IExpressionEvaluationContext context) =>
        Select(Source.Evaluate(context));
}

/// <summary>Converts a surface or stored value into one language value type.</summary>
public interface IValueCodec<out TValue>
{
    TValue Decode(object value);
}
