using FluNET.Compilation;

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
                    "FLN125",
                    "Typed compilation requires a bound program."));
            }

            context.SetTypedProgram(compiler.Compile(context.BoundProgram));
            return next(context, cancellationToken);
        }
        catch (CommandCompilationException exception)
        {
            context.CompilationDiagnostics.Add(
                exception.Code,
                CompilationPhase.Bind,
                exception.Message,
                exception.Span);
            return ValueTask.FromResult(ExecutionResult.Failed(
                ExecutionFailureKind.Binding,
                exception.Code,
                exception.Message,
                exception,
                context.Sentence));
        }
    }
}
