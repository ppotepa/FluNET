using FluNET.Context;
using FluNET.Declarative.Reconciliation;

namespace FluNET.Tests.Declarative;

[TestFixture]
public sealed class SyncCompilerTests
{
    [Test]
    public void SyncCompilerCreatesSourceToTargetContractAndIndependentReadGraph()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SyncCompilationResult result = context.CompileSync("SYNC target.json WITH desired.json BY id");

        Assert.That(result.IsValid, Is.True, string.Join(" | ", result.Diagnostics.Select(item => item.Message)));
        SyncDefinition definition = result.Definitions.Single();
        Assert.Multiple(() =>
        {
            Assert.That(definition.Goal.TargetResource, Is.EqualTo("target.json"));
            Assert.That(definition.Goal.SourceResource, Is.EqualTo("desired.json"));
            Assert.That(definition.Goal.KeyField, Is.EqualTo("id"));
            Assert.That(definition.Goal.Direction, Is.EqualTo(SyncDirection.SourceToTarget));
            Assert.That(definition.ReadCompilation.Plan!.Steps, Has.Count.EqualTo(2));
            Assert.That(definition.ReadCompilation.Plan.Steps.All(step => step.Dependencies.Count == 0), Is.True);
        });
    }

    [Test]
    public void SyncParserProtectsKeywordsInsideQuotedSql()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SyncCompilationResult result = context.CompileSync(
            "SYNC target.json WITH sql:\"SELECT id, name FROM users WHERE note = 'WITH BY'\" BY id");

        Assert.That(result.IsValid, Is.True, string.Join(" | ", result.Diagnostics.Select(item => item.Message)));
        Assert.That(result.Definitions.Single().SourceDescriptor.Reference.Kind,
            Is.EqualTo(FluNET.Language.Resources.ResourceKind.Sql));
    }

    [Test]
    public void SemicolonSeparatesSyncDefinitionsWithoutCreatingDirectionSemantics()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SyncCompilationResult result = context.CompileSync(
            "SYNC a.json WITH b.json BY id; SYNC c.json WITH d.json BY key");

        Assert.That(result.IsValid, Is.True, string.Join(" | ", result.Diagnostics.Select(item => item.Message)));
        Assert.That(result.Definitions, Has.Count.EqualTo(2));
    }
}
