using FluNET.Language;
using FluNET.Language.Binding;

namespace FluNET.Execution.Commands;

public sealed class CommandBindingContext
{
    private readonly BoundCommand _command;
    private readonly ExpressionBinder _expressions;

    public CommandBindingContext(BoundCommand command, ExpressionBinder expressions)
    {
        _command = command ?? throw new ArgumentNullException(nameof(command));
        _expressions = expressions ?? throw new ArgumentNullException(nameof(expressions));
    }

    public IExpression<TValue> Require<TValue>(FrameRoleId role) =>
        _expressions.Bind<TValue>(_command[role]);

    public IExpression<TValue> Require<TValue>(SemanticRole role) =>
        Require<TValue>((FrameRoleId)role);

    public IExpression<TValue>? Optional<TValue>(FrameRoleId role)
    {
        BoundArgument argument = _command[role];
        return argument.IsPresent ? _expressions.Bind<TValue>(argument) : null;
    }

    public IExpression<TValue>? Optional<TValue>(SemanticRole role) =>
        Optional<TValue>((FrameRoleId)role);

    public IExpression<string> RequireText(FrameRoleId role, bool preserveStructuredReferences = false) =>
        _expressions.BindText(_command[role], preserveStructuredReferences);

    public IExpression<string> RequireText(SemanticRole role, bool preserveStructuredReferences = false) =>
        RequireText((FrameRoleId)role, preserveStructuredReferences);

    public IReadOnlyList<IExpression<TValue>> Repeated<TValue>(FrameRoleId role) =>
        _expressions.BindRepeated<TValue>(_command[role]);

    public IReadOnlyList<IExpression<TValue>> Repeated<TValue>(SemanticRole role) =>
        Repeated<TValue>((FrameRoleId)role);
}
