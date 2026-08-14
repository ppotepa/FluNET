using FluNET.Context;
using FluNET.Declarative.Reconciliation;
using FluNET.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests.Telemetry;

[TestFixture]
public sealed class TelemetryTests
{
    [Test]
    public async Task CommonDispatcherEmitsMetadataWithoutCommandArguments()
    {
        InMemoryFluNetTelemetrySink sink = new();
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(services =>
            services.AddSingleton<IFluNetTelemetrySink>(sink));

        await context.ExecuteSurfaceAsync("SAY \"secret-looking-text\"");

        FluNetTelemetryEvent item = sink.Snapshot().Single(eventItem => eventItem.Category == "command");
        Assert.Multiple(() =>
        {
            Assert.That(item.Attributes["frame.id"], Is.EqualTo("core.say.text"));
            Assert.That(item.Outcome, Is.EqualTo("succeeded"));
            Assert.That(string.Join("|", item.Attributes.Values), Does.Not.Contain("secret-looking-text"));
        });
    }

    [Test]
    public async Task ReconciliationTelemetryIncludesFencingTokenAndDiffCountsOnly()
    {
        InMemoryFluNetTelemetrySink sink = new();
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SyncDefinition definition = context.CompileSync("SYNC target.json WITH desired.json BY id").Definitions.Single();
        ReconciliationLeaseContextAccessor accessor = new();
        ReconciliationDiff diff = new([new("1", ReconciliationChangeKind.Update, null, null)]);
        IReconciliationExecutor inner = new FixedExecutor(new(definition, null, null, diff, null, Array.Empty<FluNET.Execution.Planning.ExecutionStepResult>(), true, null));
        TelemetryReconciliationExecutor executor = new(inner, sink, accessor);
        using (accessor.Push(new("file:target", "test", 42, DateTimeOffset.UtcNow.AddMinutes(1))))
            await executor.RunAsync(definition);

        FluNetTelemetryEvent item = sink.Snapshot().Single();
        Assert.Multiple(() =>
        {
            Assert.That(item.Attributes["fencing.token"], Is.EqualTo("42"));
            Assert.That(item.Attributes["updates"], Is.EqualTo("1"));
            Assert.That(item.Attributes.ContainsKey("target.path"), Is.False);
        });
    }

    private sealed class FixedExecutor(ReconciliationRunResult result) : IReconciliationExecutor
    {
        public ValueTask<ReconciliationRunResult> RunAsync(SyncDefinition definition, ResourceStateSnapshot? baseline = null, CancellationToken cancellationToken = default) => ValueTask.FromResult(result);
    }
}
