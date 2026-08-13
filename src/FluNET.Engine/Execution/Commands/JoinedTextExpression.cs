namespace FluNET.Execution.Commands;

public sealed class JoinedTextExpression(IEnumerable<IExpression<string>> parts)
    : IExpression<string>
{
    private readonly IReadOnlyList<IExpression<string>> _parts =
        parts?.ToArray() ?? throw new ArgumentNullException(nameof(parts));

    public IReadOnlyList<IExpression<string>> Parts => _parts;

    public string Evaluate(IExpressionEvaluationContext context) =>
        string.Join(
            " ",
            _parts.Select(part => part.Evaluate(context))
                .Where(value => !string.IsNullOrEmpty(value)));
}
