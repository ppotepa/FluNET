using FluNET.Context;
using FluNET.Declarative.Reconciliation;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests.Declarative;

[TestFixture]
public sealed class ReconciliationMutatorTests
{
    [Test]
    public void HigherPriorityCustomMutatorOverridesBuiltInTargetContract()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(services =>
            services.AddSingleton<IReconciliationMutator, CustomMutator>());
        SyncDefinition definition = context.CompileSync("SYNC target.json WITH desired.json BY id").Definitions.Single();

        IReconciliationMutator selected = context.GetReconciliationMutatorRegistry().Resolve(definition);

        Assert.That(selected.Id, Is.EqualTo("test.custom"));
    }

    [Test]
    public void BuiltInLocalJsonMutatorProducesOrdinaryExecutionPlan()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SyncDefinition definition = context.CompileSync("SYNC target.json WITH desired.json BY id").Definitions.Single();
        DesiredStateSnapshot desired = new(
            ResourceIdentity.Parse("desired.json"),
            "id",
            [System.Text.Json.JsonSerializer.SerializeToElement(new { id = 1, name = "Ada" })]);
        ObservedStateSnapshot observed = new(
            ResourceIdentity.Parse("target.json"),
            "id",
            Array.Empty<System.Text.Json.JsonElement>());
        ReconciliationDiff diff = new ReconciliationDiffEngine().Compare(desired, observed);

        ReconciliationMutationPlan plan = context.GetReconciliationMutationPlanner().Plan(definition, desired, diff);

        Assert.Multiple(() =>
        {
            Assert.That(plan.IsValid, Is.True);
            Assert.That(plan.MutatorId, Is.EqualTo("core.reconciliation.local-json"));
            Assert.That(plan.Compilation.Plan!.Steps.Single().Command.Frame.Id.Value, Is.EqualTo("core.save.text"));
        });
    }

    private sealed class CustomMutator : IReconciliationMutator
    {
        public string Id => "test.custom";
        public int Priority => 100;
        public bool CanMutate(SyncDefinition definition) => true;
        public ReconciliationMutationPlan Plan(ReconciliationMutationRequest request) =>
            throw new NotSupportedException("Selection test only.");
    }
}
