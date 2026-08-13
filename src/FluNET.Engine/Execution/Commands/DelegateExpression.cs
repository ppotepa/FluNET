namespace FluNET.Execution.Commands;

public sealed class DelegateExpression<TValue>(
    Func<IExpressionEvaluationContext, TValue> evaluate) : IExpression<TValue>
{
    private readonly Func<IExpressionEvaluationContext, TValue> _evaluate =
        evaluate ?? throw new ArgumentNullException(nameof(evaluate));

    public TValue Evaluate(IExpressionEvaluationContext context) => _evaluate(context);
}
