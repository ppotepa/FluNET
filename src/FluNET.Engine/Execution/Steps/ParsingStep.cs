using FluNET.Compilation;
using FluNET.Prompt;

namespace FluNET.Execution.Steps;

/// <summary>Accepts the source-aware syntax already produced by ProcessedPrompt.</summary>
public sealed class ParsingStep : IExecutionStep
{
    public ValueTask<ExecutionResult> ExecuteAsync(
        ExecutionContext context,
        Func<ExecutionContext, CancellationToken, ValueTask<ExecutionResult>> next,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromResult(ExecutionResult.Failed(
                ExecutionFailureKind.Cancelled,
                "FLN201",
                "Execution was cancelled while parsing the program."));
        }

        if (!context.Prompt.IsValid)
        {
            PromptDiagnostic diagnostic = context.Prompt.Diagnostics[0];
            return ValueTask.FromResult(ExecutionResult.Failed(
                ExecutionFailureKind.Syntax,
                diagnostic.Code,
                diagnostic.Message));
        }

        if (context.Prompt.Syntax.Commands.Count == 0)
        {
            return ValueTask.FromResult(ExecutionResult.Failed(
                ExecutionFailureKind.Syntax,
                CompilationDiagnosticCodes.EmptyProgram,
                "Empty prompt does not contain a command."));
        }

        context.Program = new FluNetProgram(context.Prompt);
        return next(context, cancellationToken);
    }
}
