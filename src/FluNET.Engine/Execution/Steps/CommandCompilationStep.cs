using FluNET.Compilation;
using FluNET.Execution.Commands;
using FluNET.Language.Binding;
using FluNET.Prompt;

namespace FluNET.Execution.Steps;

public sealed class CommandCompilationStep(TypedProgramCompiler compiler) : IExecutionStep
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
                return ValueTask.FromResult(ExecutionResult.Failed(
                    ExecutionFailureKind.Binding,
                    CompilationDiagnosticCodes.CompilationFailure,
                    "Typed compilation requires a bound program."));
            }

            CompileConditions(context.BoundProgram);
            context.SetTypedProgram(compiler.Compile(context.BoundProgram));
            return next(context, cancellationToken);
        }
        catch (CommandCompilationException exception)
        {
            context.CompilationDiagnostics.Add(
                exception.Code,
                CompilationPhase.Compile,
                exception.Message,
                exception.Span);
            return ValueTask.FromResult(ExecutionResult.Failed(
                ExecutionFailureKind.Binding,
                exception.Code,
                exception.Message,
                exception));
        }
        catch (Exception exception) when (
            exception is FormatException or NotSupportedException or InvalidOperationException)
        {
            string message = $"Invalid condition expression: {exception.Message}";
            context.CompilationDiagnostics.Add(
                "FLN154",
                CompilationPhase.Compile,
                message,
                context.Prompt.Syntax.Span);
            return ValueTask.FromResult(ExecutionResult.Failed(
                ExecutionFailureKind.Validation,
                "FLN154",
                message,
                exception));
        }
    }

    private static void CompileConditions(BoundProgram program)
    {
        foreach (BoundCommand command in program.Commands)
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
    }
}
