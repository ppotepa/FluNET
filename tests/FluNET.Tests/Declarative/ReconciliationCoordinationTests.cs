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

    [Test]
    public async Task CompletedRunIsRejectedWhenFinalOwnershipCannotBeConfirmed()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SyncDefinition definition = context.CompileSync("SYNC target.json WITH desired.json BY id").Definitions.Single();
        ImmediateExecutor inner = new();
        LoseOnRenewLeaseStore leases = new();
        ReconciliationCoordinator coordinator = new(
            inner,
            leases,
            new ReconciliationLeaseContextAccessor(),
            new ReconciliationCoordinationOptions(TimeSpan.FromSeconds(30)));

        ReconciliationRunResult result = await coordinator.RunAsync(definition);

        Assert.Multiple(() =>
        {
            Assert.That(inner.Calls, Is.EqualTo(1));
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.TypeOf<ReconciliationLeaseLostException>());
        });
    }

    private sealed class BlockingExecutor : IReconciliationExecutor
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<ReconciliationRunResult> RunAsync(SyncDefinition definition, ResourceStateSnapshot? baseline = null, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return Success(definition);
        }
    }

    private sealed class ImmediateExecutor : IReconciliationExecutor
    {
        public int Calls { get; private set; }
        public ValueTask<ReconciliationRunResult> RunAsync(
            SyncDefinition definition,
            ResourceStateSnapshot? baseline = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return ValueTask.FromResult(Success(definition));
        }
    }

    private sealed class LoseOnRenewLeaseStore : IReconciliationLeaseStore
    {
        public ValueTask<ReconciliationLease?> TryAcquireAsync(
            string resourceIdentity,
            string ownerId,
            TimeSpan ttl,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<ReconciliationLease?>(new(
                resourceIdentity,
                ownerId,
                17,
                DateTimeOffset.UtcNow.Add(ttl)));
        }

        public ValueTask<ReconciliationLease?> RenewAsync(
            ReconciliationLease lease,
            TimeSpan ttl,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<ReconciliationLease?>(null);
        }

        public ValueTask ReleaseAsync(ReconciliationLease lease, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
    }

    private static ReconciliationRunResult Success(SyncDefinition definition) =>
        new(
            definition,
            null,
            null,
            null,
            null,
            Array.Empty<FluNET.Execution.Planning.ExecutionStepResult>(),
            false,
            null);
}
