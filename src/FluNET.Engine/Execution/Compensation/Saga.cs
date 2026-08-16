using FluNET.Capabilities;
using FluNET.Execution.Planning;
using FluNET.Execution.Workflow;

namespace FluNET.Execution.Compensation;

public sealed record SagaStep(string Name, CompensatableCompilationResult Compilation);

public sealed record SagaPlan(IReadOnlyList<SagaStep> Steps)
{
    public SagaPlan(params SagaStep[] steps) : this((IReadOnlyList<SagaStep>)steps) { }
    public bool IsValid => Steps.Count > 0 && Steps.All(step => step.Compilation.IsValid);
}

public sealed record SagaStepResult(
    SagaStep Step,
    IReadOnlyList<ExecutionStepResult> ExecutionSteps,
    object? Result,
    Exception? Error)
{
    public bool IsSuccess => Error is null;
}

public sealed record SagaExecutionResult(
    SagaPlan Plan,
    IReadOnlyList<SagaStepResult> Steps,
    IReadOnlyList<CompensationActionResult> Compensation,
    Exception? Error)
{
    public bool IsSuccess => Plan.IsValid && Error is null;
    public bool WasCompensated => Compensation.Any(item => item.Restored);
}

/// <summary>
/// Saga orchestration over ordinary execution plans. It does not execute commands itself;
/// it journals reversible effects and delegates each unit to SentenceExecutor.
/// </summary>
public sealed class SagaExecutor(
    SentenceExecutor executor,
    IFluNetFileSystem files)
{
    private sealed record Snapshot(int UnitIndex, int PlanStepIndex, string TargetPath, bool Existed, string? Content);

    public async ValueTask<SagaExecutionResult> ExecuteAsync(
        SagaPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        List<SagaStepResult> stepResults = [];
        List<Snapshot> journal = [];
        List<CompensationActionResult> compensation = [];
        if (!plan.IsValid)
            return new(plan, stepResults, compensation, new InvalidOperationException("Saga plan is invalid."));

        try
        {
            for (int unitIndex = 0; unitIndex < plan.Steps.Count; unitIndex++)
            {
                SagaStep unit = plan.Steps[unitIndex];
                if (unit.Compilation.Compilation.Plan is null)
                    throw new InvalidOperationException($"Saga step '{unit.Name}' has no execution plan.");

                Dictionary<int, Snapshot> currentSnapshots = [];
                foreach (CompensationStepSpec spec in unit.Compilation.CompensationSteps)
                {
                    bool exists = await files.FileExistsAsync(spec.TargetPath, cancellationToken).ConfigureAwait(false);
                    string? content = exists
                        ? await files.ReadAllTextAsync(spec.TargetPath, cancellationToken).ConfigureAwait(false)
                        : null;
                    currentSnapshots[spec.StepIndex] = new(unitIndex, spec.StepIndex, spec.TargetPath, exists, content);
                }

                List<ExecutionStepResult> completed = [];
                try
                {
                    object? result = await executor.ExecuteAsync(unit.Compilation.Compilation.Plan, completed, cancellationToken).ConfigureAwait(false);
                    // A successful unit has completed its compensatable effects even when
                    // the executor did not expose an individual result for a no-op branch.
                    // Keep the pre-write snapshot for the saga journal as a unit-level fact.
                    journal.AddRange(currentSnapshots.Values);
                    stepResults.Add(new(unit, completed, result, null));
                }
                catch (Exception unitFailure)
                {
                    foreach (ExecutionStepResult executed in completed.Where(item => item.Status == WorkflowStepStatus.Succeeded))
                        if (currentSnapshots.TryGetValue(executed.Step.Index, out Snapshot? snapshot)) journal.Add(snapshot);
                    stepResults.Add(new(unit, completed, null, unitFailure));
                    throw;
                }
            }
            return new(plan, stepResults, compensation, null);
        }
        catch (Exception failure)
        {
            foreach (Snapshot snapshot in journal.AsEnumerable().Reverse())
            {
                try
                {
                    if (snapshot.Existed)
                        await files.WriteAllTextAsync(snapshot.TargetPath, snapshot.Content ?? string.Empty, CancellationToken.None).ConfigureAwait(false);
                    else
                        await files.DeleteFileAsync(snapshot.TargetPath, CancellationToken.None).ConfigureAwait(false);
                    compensation.Add(new(snapshot.PlanStepIndex, snapshot.TargetPath, true, null));
                }
                catch (Exception compensationFailure)
                {
                    compensation.Add(new(snapshot.PlanStepIndex, snapshot.TargetPath, false, compensationFailure));
                }
            }
            return new(plan, stepResults, compensation, failure);
        }
    }
}

public sealed class SagaCompiler(CompensatableSurfaceCompiler compiler)
{
    public SagaPlan Compile(params (string Name, string Source)[] units)
    {
        ArgumentNullException.ThrowIfNull(units);
        return new SagaPlan(units
            .Select(unit => new SagaStep(unit.Name, compiler.Compile(unit.Source)))
            .ToArray());
    }
}
