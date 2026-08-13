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
        if (binary.Operator.Equals("AND", StringComparison.OrdinalIgnoreCase))
        {
            return context => ToBoolean(left(context)) && ToBoolean(right(context));
        }
        throw new NotSupportedException($"Binary condition '{binary.Operator}' is not available in this build.");
    }
}
