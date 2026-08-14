using FluNET.Declarative.Reconciliation;
using System.Text.Json;

namespace FluNET.Tests.Declarative;

[TestFixture]
public sealed class ReconciliationDiffTests
{
    [Test]
    public void TwoWayDiffClassifiesCreateUpdateDeleteAndUnchanged()
    {
        DesiredStateSnapshot desired = Desired("""
[
  {"id":1,"name":"same"},
  {"id":2,"name":"new"},
  {"id":3,"name":"create"}
]
""");
        ObservedStateSnapshot observed = Observed("""
[
  {"id":1,"name":"same"},
  {"id":2,"name":"old"},
  {"id":4,"name":"delete"}
]
""");

        ReconciliationDiff diff = new ReconciliationDiffEngine().Compare(desired, observed);

        Assert.Multiple(() =>
        {
            Assert.That(diff.Creates, Is.EqualTo(1));
            Assert.That(diff.Updates, Is.EqualTo(1));
            Assert.That(diff.Deletes, Is.EqualTo(1));
            Assert.That(diff.Unchanged, Is.EqualTo(1));
            Assert.That(diff.Conflicts, Is.Zero);
        });
    }

    [Test]
    public void ThreeWayDiffConflictsOnlyWhenBothSidesDivergedDifferently()
    {
        DesiredStateSnapshot desired = Desired("[{\"id\":1,\"name\":\"desired\"}]");
        ObservedStateSnapshot observed = Observed("[{\"id\":1,\"name\":\"target\"}]");
        ObservedStateSnapshot baseline = Observed("[{\"id\":1,\"name\":\"base\"}]");

        ReconciliationDiff diff = new ReconciliationDiffEngine().Compare(desired, observed, baseline);

        Assert.Multiple(() =>
        {
            Assert.That(diff.Conflicts, Is.EqualTo(1));
            Assert.That(diff.Changes.Single().Kind, Is.EqualTo(ReconciliationChangeKind.Conflict));
        });
    }

    [Test]
    public void TargetDriftWithUnchangedDesiredIsAnUpdateNotAConflict()
    {
        DesiredStateSnapshot desired = Desired("[{\"id\":1,\"name\":\"base\"}]");
        ObservedStateSnapshot observed = Observed("[{\"id\":1,\"name\":\"drift\"}]");
        ObservedStateSnapshot baseline = Observed("[{\"id\":1,\"name\":\"base\"}]");

        ReconciliationDiff diff = new ReconciliationDiffEngine().Compare(desired, observed, baseline);

        Assert.That(diff.Changes.Single().Kind, Is.EqualTo(ReconciliationChangeKind.Update));
    }

    private static DesiredStateSnapshot Desired(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return new(new ResourceIdentity("memory", "desired"), "id", document.RootElement.EnumerateArray().Select(item => item.Clone()));
    }

    private static ObservedStateSnapshot Observed(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return new(new ResourceIdentity("memory", "observed"), "id", document.RootElement.EnumerateArray().Select(item => item.Clone()));
    }
}
