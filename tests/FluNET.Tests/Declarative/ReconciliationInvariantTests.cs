using FluNET.Declarative.Reconciliation;
using System.Text.Json;

namespace FluNET.Tests.Declarative;

[TestFixture]
public sealed class ReconciliationInvariantTests
{
    [Test]
    public void IdenticalSnapshotsAlwaysProduceOnlyUnchangedRecords()
    {
        Random random = new(1729);
        ReconciliationDiffEngine engine = new();
        for (int iteration = 0; iteration < 128; iteration++)
        {
            int count = random.Next(0, 80);
            JsonElement[] values = Enumerable.Range(0, count)
                .Select(id => JsonSerializer.SerializeToElement(new
                {
                    id,
                    value = random.Next(-100_000, 100_000),
                    flag = random.Next(0, 2) == 1
                }))
                .ToArray();
            DesiredStateSnapshot desired = new(new("memory", "desired"), "id", values);
            ObservedStateSnapshot observed = new(new("memory", "observed"), "id", values);

            ReconciliationDiff diff = engine.Compare(desired, observed);

            Assert.Multiple(() =>
            {
                Assert.That(diff.HasMutations, Is.False);
                Assert.That(diff.HasConflicts, Is.False);
                Assert.That(diff.Unchanged, Is.EqualTo(count));
            });
        }
    }

    [Test]
    public void DesiredSnapshotIsAStableFixedPointAfterApply()
    {
        Random random = new(7331);
        ReconciliationDiffEngine engine = new();
        for (int iteration = 0; iteration < 128; iteration++)
        {
            int count = random.Next(1, 100);
            JsonElement[] desiredValues = Enumerable.Range(0, count)
                .Select(id => JsonSerializer.SerializeToElement(new { id, value = random.Next() }))
                .ToArray();
            JsonElement[] observedValues = desiredValues
                .Where((_, index) => index % 3 != 0)
                .Select((value, index) => index % 5 == 0
                    ? JsonSerializer.SerializeToElement(new { id = value.GetProperty("id").GetInt32(), value = -1 })
                    : value)
                .ToArray();
            DesiredStateSnapshot desired = new(new("memory", "desired"), "id", desiredValues);
            ObservedStateSnapshot observed = new(new("memory", "target"), "id", observedValues);
            Assert.That(engine.Compare(desired, observed).HasMutations, Is.True);

            ObservedStateSnapshot afterApply = new(
                observed.Identity,
                "id",
                desired.Records.Select(record => record.Value));
            ReconciliationDiff fixedPoint = engine.Compare(desired, afterApply);

            Assert.That(fixedPoint.HasChanges, Is.False);
        }
    }

    [Test]
    [Category("Stress")]
    public void TenThousandKeyedRecordsRemainDeterministicallyOrderedAndStable()
    {
        const int count = 10_000;
        JsonElement[] values = Enumerable.Range(0, count)
            .Reverse()
            .Select(id => JsonSerializer.SerializeToElement(new { id, payload = $"row-{id}" }))
            .ToArray();
        DesiredStateSnapshot desired = new(new("memory", "large"), "id", values);
        ObservedStateSnapshot observed = new(new("memory", "large-target"), "id", desired.Records.Select(record => record.Value));

        ReconciliationDiff diff = new ReconciliationDiffEngine().Compare(desired, observed);
        string[] actualKeys = desired.Records.Select(record => record.Key).ToArray();
        string[] orderedKeys = actualKeys.OrderBy(key => key, StringComparer.Ordinal).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(desired.Records, Has.Count.EqualTo(count));
            Assert.That(actualKeys, Is.EqualTo(orderedKeys));
            Assert.That(diff.Unchanged, Is.EqualTo(count));
            Assert.That(diff.HasChanges, Is.False);
        });
    }

    [Test]
    public async Task OnlyOneConcurrentLeaseAcquisitionWinsForOneTarget()
    {
        InMemoryReconciliationLeaseStore store = new();
        Task<ReconciliationLease?>[] attempts = Enumerable.Range(0, 64)
            .Select(index => store.TryAcquireAsync(
                "file:shared-target",
                $"owner-{index}",
                TimeSpan.FromSeconds(30)).AsTask())
            .ToArray();

        ReconciliationLease?[] leases = await Task.WhenAll(attempts);

        Assert.That(leases.Count(lease => lease is not null), Is.EqualTo(1));
    }

    [Test]
    public void DuplicateKeysAreRejectedForLargeInputsBeforeDiffing()
    {
        JsonElement[] values = Enumerable.Range(0, 1_000)
            .Select(id => JsonSerializer.SerializeToElement(new { id }))
            .Append(JsonSerializer.SerializeToElement(new { id = 999 }))
            .ToArray();

        Assert.Throws<FormatException>(() =>
            _ = new DesiredStateSnapshot(new ResourceIdentity("memory", "duplicates"), "id", values));
    }
}
