using FluNET.Compilation;

namespace FluNET.Execution.Steps;

public sealed class TypeValidationStep : IExecutionStep
{
    private readonly TypedProgramTypeValidator _validator = new();

    public ValueTask<ExecutionResult> ExecuteAsync(
        ExecutionContext context,
        Func<ExecutionContext, CancellationToken, ValueTask<ExecutionResult>> next,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            TypedProgram program = context.GetTypedProgram()
                ?? throw new CommandCompilationException(
                    CompilationDiagnosticCodes.CompilationFailure,
                    "Type checking requires a typed program.",
                    context.Prompt.Syntax.Span);
            _validator.Validate(program);
            return next(context, cancellationToken);
        }
        catch (CommandCompilationException exception)
        {
            context.CompilationDiagnostics.Add(
                exception.Code,
                CompilationPhase.TypeCheck,
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
