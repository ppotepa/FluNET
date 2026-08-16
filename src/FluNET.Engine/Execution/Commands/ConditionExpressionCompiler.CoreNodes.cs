using FluNET.Prompt.Expressions;
using System.Text.RegularExpressions;

namespace FluNET.Execution.Commands;

public sealed partial class ConditionExpressionCompiler
{
    public static string NormalizeNaturalCondition(string source)
    {
        HashSet<string> languageWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "AND", "OR", "NOT", "TRUE", "FALSE", "NULL", "CONTAINS", "STARTS", "ENDS", "WITH", "IN"
        };

        return Regex.Replace(
            source,
            @"(?<![\[\w])(?<name>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)(?![\]\w])",
            match => languageWords.Contains(match.Groups["name"].Value)
                ? match.Value
                : $"[{match.Value}]",
            RegexOptions.CultureInvariant);
    }

    private static Func<IExpressionEvaluationContext, object?> CompileNode(
        ExpressionSyntax syntax,
        ISet<string> variables) => syntax switch
    {
        VariableExpressionSyntax variable => CompileVariable(variable, variables),
        LiteralExpressionSyntax literal => _ => ParseLiteral(literal.Text),
        ParenthesizedExpressionSyntax grouped => CompileNode(grouped.Expression, variables),
        BinaryExpressionSyntax binary => CompileBinary(binary, variables),
        UnaryExpressionSyntax unary => CompileUnary(unary, variables),
        PropertyExpressionSyntax property => CompileProperty(property, variables),
        IndexExpressionSyntax index => CompileIndex(index, variables),
        ListExpressionSyntax list => CompileList(list, variables),
        ObjectExpressionSyntax value => CompileObject(value, variables),
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
        if (DataUnits.TryParse(value, out decimal sized)) return sized;
        if (decimal.TryParse(
            value,
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out decimal number))
        {
            return number;
        }
        return value;
    }

    private static bool ToBoolean(object? value) => value switch
    {
        bool boolean => boolean,
        string text when bool.TryParse(text, out bool boolean) => boolean,
        string text => !string.IsNullOrWhiteSpace(text),
        null => false,
        _ when TryDecimal(value, out decimal number) => number != 0m,
        _ => true
    };

    private static bool TryDecimal(object? value, out decimal number)
    {
        switch (value)
        {
            case decimal direct:
                number = direct;
                return true;
            case string text when decimal.TryParse(
                text.Trim().Trim('"', '\''),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out decimal parsed):
                number = parsed;
                return true;
            case byte direct:
                number = direct;
                return true;
            case sbyte direct:
                number = direct;
                return true;
            case short direct:
                number = direct;
                return true;
            case ushort direct:
                number = direct;
                return true;
            case int direct:
                number = direct;
                return true;
            case uint direct:
                number = direct;
                return true;
            case long direct:
                number = direct;
                return true;
            case ulong direct:
                number = direct;
                return true;
            case float direct when float.IsFinite(direct):
                try
                {
                    number = (decimal)direct;
                    return true;
                }
                catch (OverflowException)
                {
                    break;
                }
            case double direct when double.IsFinite(direct):
                try
                {
                    number = (decimal)direct;
                    return true;
                }
                catch (OverflowException)
                {
                    break;
                }
        }

        number = default;
        return false;
    }
}
