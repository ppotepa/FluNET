using FluNET.Prompt.Expressions;

namespace FluNET.Execution.Commands;

public sealed partial class ConditionExpressionCompiler
{
    private static Func<IExpressionEvaluationContext, object?> CompileBinary(
        BinaryExpressionSyntax binary,
        ISet<string> variables)
    {
        Func<IExpressionEvaluationContext, object?> left = CompileNode(binary.Left, variables);
        Func<IExpressionEvaluationContext, object?> right = CompileNode(binary.Right, variables);
        string operation = binary.Operator.ToUpperInvariant();

        return operation switch
        {
            "??" => context => left(context) ?? right(context),
            "AND" => context => ToBoolean(left(context)) && ToBoolean(right(context)),
            "OR" => context => ToBoolean(left(context)) || ToBoolean(right(context)),
            "==" => context => EqualValues(left(context), right(context)),
            "!=" => context => !EqualValues(left(context), right(context)),
            "<" => context => CompareValues(left(context), right(context)) < 0,
            "<=" => context => CompareValues(left(context), right(context)) <= 0,
            ">" => context => CompareValues(left(context), right(context)) > 0,
            ">=" => context => CompareValues(left(context), right(context)) >= 0,
            "+" => context => Numeric(left(context), right(context), static (a, b) => a + b, "+"),
            "-" => context => Numeric(left(context), right(context), static (a, b) => a - b, "-"),
            "*" => context => Numeric(left(context), right(context), static (a, b) => a * b, "*"),
            "/" => context => Divide(left(context), right(context)),
            _ => throw new NotSupportedException($"Binary condition '{binary.Operator}' is not available in this build.")
        };
    }

    private static bool EqualValues(object? left, object? right)
    {
        if (TryDecimal(left, out decimal leftNumber) &&
            TryDecimal(right, out decimal rightNumber))
        {
            return leftNumber == rightNumber;
        }
        return Equals(left, right);
    }

    private static int CompareValues(object? left, object? right)
    {
        if (TryDecimal(left, out decimal leftNumber) &&
            TryDecimal(right, out decimal rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }
        if (left is string leftText && right is string rightText)
        {
            return string.Compare(leftText, rightText, StringComparison.Ordinal);
        }
        throw new InvalidOperationException(
            $"Values '{left?.GetType().Name ?? "null"}' and '{right?.GetType().Name ?? "null"}' cannot be ordered.");
    }

    private static decimal Numeric(
        object? left,
        object? right,
        Func<decimal, decimal, decimal> operation,
        string symbol)
    {
        if (!TryDecimal(left, out decimal leftNumber) ||
            !TryDecimal(right, out decimal rightNumber))
        {
            throw new InvalidOperationException(
                $"Operator '{symbol}' requires Number operands.");
        }
        return operation(leftNumber, rightNumber);
    }

    private static decimal Divide(object? left, object? right)
    {
        if (!TryDecimal(left, out decimal leftNumber) ||
            !TryDecimal(right, out decimal rightNumber))
        {
            throw new InvalidOperationException("Operator '/' requires Number operands.");
        }
        if (rightNumber == 0m)
        {
            throw new DivideByZeroException("A condition expression cannot divide by zero.");
        }
        return leftNumber / rightNumber;
    }
}
