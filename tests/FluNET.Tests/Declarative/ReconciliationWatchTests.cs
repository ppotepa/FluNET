using FluNET.Context;
using FluNET.Declarative.Reconciliation;

namespace FluNET.Tests.Declarative;

[TestFixture]
public sealed class ReconciliationWatchTests
{
    [Test]
    public void WatchCompilerEmbedsSyncDefinition()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        const string source = """
WATCH users.changed
    WHEN updated
        SYNC target.json WITH desired.json BY id
""";
        ReconciliationWatchCompilationResult result = context.CompileReconciliationWatches(source);
        Assert.That(result.IsValid, Is.True, string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Multiple(() =>
        {
            Assert.That(result.Watches, Has.Count.EqualTo(1));
            Assert.That(result.Watches[0].Trigger.Event, Is.EqualTo("updated"));
            Assert.That(result.Watches[0].SyncDefinitions.Single().Goal.KeyField, Is.EqualTo("id"));
        });
    }
}
