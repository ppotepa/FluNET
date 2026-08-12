namespace FluNET.Execution;

public sealed class ExecutionPipeline
{
    private readonly List<IExecutionStep> _steps = [];

    public ExecutionPipeline AddStep(IExecutionStep step)
    {
        _steps.Add(step ?? throw new ArgumentNullException(nameof(step)));
        return this;
    }

    public ExecutionResult Execute(ExecutionContext context) =>
        ExecuteAsync(context).AsTask().GetAwaiter().GetResult();

    public ValueTask<ExecutionResult> ExecuteAsync(
        ExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return BuildChain()(context, cancellationToken);
    }

    private Func<ExecutionContext, CancellationToken, ValueTask<ExecutionResult>> BuildChain()
    {
        Func<ExecutionContext, CancellationToken, ValueTask<ExecutionResult>> chain = TerminalStepAsync;

        for (int index = _steps.Count - 1; index >= 0; index--)
        {
            IExecutionStep current = _steps[index];
            Func<ExecutionContext, CancellationToken, ValueTask<ExecutionResult>> next = chain;
            chain = (context, cancellationToken) => current.ExecuteAsync(context, next, cancellationToken);
        }

        return chain;
    }

    private static ValueTask<ExecutionResult> TerminalStepAsync(
        ExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromResult(ExecutionResult.Failed(
                ExecutionFailureKind.Cancelled,
                "FLN201",
                "Execution was cancelled."));
        }

        if (context.ValidationResult is { IsValid: false } validation)
        {
            return ValueTask.FromResult(ExecutionResult.Failed(validation));
        }

        if (context.Exception is not null)
        {
            return ValueTask.FromResult(ExecutionResult.Failed(
                ExecutionFailureKind.Execution,
                "FLN200",
                context.Exception.Message,
                context.Exception,
                context.Sentence));
        }

        if (context.Sentence is null)
        {
            return ValueTask.FromResult(ExecutionResult.Failed(
                ExecutionFailureKind.Internal,
                "FLN202",
                "The pipeline completed without producing a sentence."));
        }

        return ValueTask.FromResult(ExecutionResult.Success(
            context.Sentence,
            context.Result,
            context.Plan,
            context.StepResults));
    }
}
