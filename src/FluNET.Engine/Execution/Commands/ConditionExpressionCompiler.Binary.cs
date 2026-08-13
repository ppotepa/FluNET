using FluNET.Prompt.Expressions;

namespace FluNET.Execution.Commands;

public sealed partial class ConditionExpressionCompiler
{
    private static Func<IExpressionEvaluationContext, object?> CompileBinary(
        BinaryExpressionSyntax binary,
        ISet<string> variables) =>
        throw new NotSupportedException($"Binary condition '{binary.Operator}' is not available in this build.");
}
