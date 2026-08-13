namespace FluNET.Execution.Commands;

public sealed record CompiledCondition(
    IExpression<bool> Expression,
    IReadOnlySet<string> VariableReferences);
