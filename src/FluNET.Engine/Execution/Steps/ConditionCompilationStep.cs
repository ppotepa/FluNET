using FluNET.Compilation;
using FluNET.Execution.Commands;
using FluNET.Language.Binding;
using FluNET.Prompt;

namespace FluNET.Execution.Steps;

public sealed class ConditionCompilationStep : IExecutionStep
{
    public ValueTask<ExecutionResult> ExecuteAsync(
        ExecutionContext context,
        Func<ExecutionContext, CancellationToken, ValueTask<ExecutionResult>> next,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (context.BoundProgram is null)
            {
                return next(context, cancellationToken);
            }

            foreach (BoundCommand command in context.BoundProgram.Commands)
            {
                foreach (CommandModifierSyntax modifier in command.Syntax.Modifiers.Where(item =>
                    item.Kind == CommandModifierKind.Condition))
                {
                    if (modifier.Values.Count == 0)
                    {
                        throw new FormatException("IF must be followed by a condition expression.");
                    }
                    string source = string.Join(" ", modifier.Values.Select(token => token.Text));
                    ConditionExpressionCache.GetOrCompile(source);
                }
            }
            return next(context, cancellationToken);
        }
        catch (Exception exception) when (
            exception is FormatException or NotSupportedException or InvalidOperationException)
        {
            string message = $"Invalid condition expression: {exception.Message}";
            context.CompilationDiagnostics.Add(
                "FLN154",
                CompilationPhase.Validate,
                message,
                context.Prompt.Syntax.Span);
            return ValueTask.FromResult(ExecutionResult.Failed(
                ExecutionFailureKind.Validation,
                "FLN154",
                message,
                exception,
                context.Sentence));
        }
    }
}
