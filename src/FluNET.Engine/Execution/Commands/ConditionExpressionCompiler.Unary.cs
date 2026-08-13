using FluNET.Prompt.Expressions;

namespace FluNET.Execution.Commands;

public sealed partial class ConditionExpressionCompiler
{
    private static Func<IExpressionEvaluationContext, object?> CompileUnary(
        UnaryExpressionSyntax unary,
        ISet<string> variables)
    {
        Func<IExpressionEvaluationContext, object?> operand = CompileNode(unary.Operand, variables);
        if (unary.Operator.Equals("NOT", StringComparison.OrdinalIgnoreCase) || unary.Operator == "!")
        {
            return context => !ToBoolean(operand(context));
        }
        throw new NotSupportedException($"Unknown unary operator '{unary.Operator}'.");
    }
}
