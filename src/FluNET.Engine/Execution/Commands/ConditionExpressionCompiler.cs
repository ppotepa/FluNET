using FluNET.Prompt.Expressions;

namespace FluNET.Execution.Commands;

public sealed partial class ConditionExpressionCompiler
{
    public CompiledCondition Compile(ExpressionSyntax syntax)
    {
        ArgumentNullException.ThrowIfNull(syntax);
        HashSet<string> variables = new(StringComparer.OrdinalIgnoreCase);
        Func<IExpressionEvaluationContext, object?> node = CompileNode(syntax, variables);
        return new CompiledCondition(
            new DelegateExpression<bool>(context => ToBoolean(node(context))),
            variables);
    }
}
