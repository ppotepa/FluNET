using FluNET.Compilation;
using FluNET.Language.Binding;
using FluNET.Syntax.Validation;

namespace FluNET.Execution.Steps;

/// <summary>Runs frame and slot validation for the typed bound program.</summary>
public sealed class SemanticValidationStep(SemanticProgramValidator validator) : IExecutionStep
{
    public ValueTask<ExecutionResult> ExecuteAsync(
        ExecutionContext context,
        Func<ExecutionContext, CancellationToken, ValueTask<ExecutionResult>> next,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.BoundProgram is null)
        {
            return ValueTask.FromResult(ExecutionResult.Failed(
                ExecutionFailureKind.Internal,
                "FLN202",
                "No bound program is available for semantic validation."));
        }

        DiagnosticBag diagnostics = validator.Validate(context.BoundProgram);
        context.CompilationDiagnostics.AddRange(diagnostics);
        if (!diagnostics.HasErrors)
        {
            context.ValidationResult = ValidationResult.Success();
            return next(context, cancellationToken);
        }

        CompilationDiagnostic first = diagnostics.First(diagnostic =>
            diagnostic.Severity == CompilationDiagnosticSeverity.Error);
        string message = string.Join(" ", diagnostics
            .Where(diagnostic => diagnostic.Severity == CompilationDiagnosticSeverity.Error)
            .Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
        context.ValidationResult = ValidationResult.Failure(message);
        return ValueTask.FromResult(ExecutionResult.Failed(
            ExecutionFailureKind.Validation,
            first.Code,
            message));
    }
}
