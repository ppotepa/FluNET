using FluNET.Context;
using FluNET.Declarative.Reconciliation;

namespace FluNET.Tests.Declarative;

[TestFixture]
public sealed class ReconciliationCoordinationTests
{
    [Test]
    public async Task FencingTokenIncreasesAcrossSuccessfulAcquisitions()
    {
        InMemoryReconciliationLeaseStore store = new();
        ReconciliationLease first = (await store.TryAcquireAsync("file:target", "one", TimeSpan.FromSeconds(5)))!;
        await store.ReleaseAsync(first);
        ReconciliationLease second = (await store.TryAcquireAsync("file:target", "two", TimeSpan.FromSeconds(5)))!;
        Assert.That(second.FencingToken, Is.GreaterThan(first.FencingToken));
    }

    [Test]
    public async Task ConcurrentRunForSameTargetIsRejectedWhileLeaseIsHeld()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SyncDefinition definition = context.CompileSync("SYNC target.json WITH desired.json BY id").Definitions.Single();
        BlockingExecutor inner = new();
        InMemoryReconciliationLeaseStore store = new();
        ReconciliationCoordinator coordinator = new(inner, store, new ReconciliationLeaseContextAccessor(), new ReconciliationCoordinationOptions(TimeSpan.FromSeconds(5)));

        Task<ReconciliationRunResult> first = coordinator.RunAsync(definition).AsTask();
        await inner.Started.Task;
        ReconciliationRunResult second = await coordinator.RunAsync(definition);

        Assert.That(second.Error, Is.TypeOf<ReconciliationLeaseUnavailableException>());
        inner.Release.TrySetResult();
        Assert.That((await first).IsSuccess, Is.True);
    }

    private sealed class BlockingExecutor : IReconciliationExecutor
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<ReconciliationRunResult> RunAsync(SyncDefinition definition, ResourceStateSnapshot? baseline = null, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return new(definition, null, null, null, null, Array.Empty<FluNET.Execution.Planning.ExecutionStepResult>(), false, null);
        }
    }
}
