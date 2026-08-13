using FluNET.Prompt.Expressions;

namespace FluNET.Execution.Commands;

public sealed partial class ConditionExpressionCompiler
{
    private static Func<IExpressionEvaluationContext, object?> CompileNode(
        ExpressionSyntax syntax,
        ISet<string> variables) => syntax switch
    {
        VariableExpressionSyntax variable => CompileVariable(variable, variables),
        LiteralExpressionSyntax literal => _ => ParseLiteral(literal.Text),
        ParenthesizedExpressionSyntax grouped => CompileNode(grouped.Expression, variables),
        BinaryExpressionSyntax binary => CompileBinary(binary, variables),
        UnaryExpressionSyntax unary => CompileUnary(unary, variables),
        _ => throw new NotSupportedException($"Unsupported condition node '{syntax.GetType().Name}'.")
    };

    private static Func<IExpressionEvaluationContext, object?> CompileVariable(
        VariableExpressionSyntax variable,
        ISet<string> variables)
    {
        variables.Add(variable.Name);
        string reference = $"[{variable.Name}]";
        return context => context.Variables.Resolve<object>(reference)
            ?? throw new InvalidOperationException($"Condition variable {reference} was not found.");
    }

    private static object ParseLiteral(string text)
    {
        string value = text.Trim('"', '\'');
        if (bool.TryParse(value, out bool boolean)) return boolean;
        if (decimal.TryParse(value, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out decimal number)) return number;
        return value;
    }

    private static bool ToBoolean(object? value) => value switch
    {
        bool boolean => boolean,
        decimal number => number != 0m,
        string text when bool.TryParse(text, out bool boolean) => boolean,
        string text => !string.IsNullOrWhiteSpace(text),
        null => false,
        _ => true
    };
}
