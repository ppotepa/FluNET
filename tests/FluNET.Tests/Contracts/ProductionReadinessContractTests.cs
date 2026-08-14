using FluNET.Declarative.Reconciliation;
using FluNET.Language;
using FluNET.Language.Contracts;
using FluNET.Persistence;
using FluNET.Telemetry;

namespace FluNET.Tests.Contracts;

[TestFixture]
public sealed class ProductionReadinessContractTests
{
    [Test]
    public void OneZeroCandidateIncludesNewProductionExtensionPoints()
    {
        ExtensionApiContractManifest manifest = ExtensionApiContractManifest.Candidate1_0;
        Assert.Multiple(() =>
        {
            Assert.That(manifest.Contains(typeof(IResourceObserver)), Is.True);
            Assert.That(manifest.Contains(typeof(IReconciliationMutator)), Is.True);
            Assert.That(manifest.Contains(typeof(IReconciliationStateStore)), Is.True);
            Assert.That(manifest.Contains(typeof(IReconciliationLeaseStore)), Is.True);
            Assert.That(manifest.Contains(typeof(IReconciliationCheckpointStore)), Is.True);
            Assert.That(manifest.Contains(typeof(IFluNetTelemetrySink)), Is.True);
        });
    }

    [Test]
    public void DurableFormatIdsAndVersionsAreUniqueAndPositive()
    {
        DurableFormatContractManifest manifest = DurableFormatContractManifest.Candidate1_0;
        Assert.That(manifest.Formats.Select(item => item.Id), Is.Unique);
        Assert.That(manifest.Formats.All(item => item.Version > 0), Is.True);
    }

    [Test]
    public void StaticReleaseVerifierAcceptsSourceContractWithoutPretendingVersionIsPromoted()
    {
        ReleaseContractReport report = ReleaseCandidateVerifier.Verify1_0(SurfaceLanguage.CreateRuntime().Language);
        Assert.Multiple(() =>
        {
            Assert.That(report.IsStaticallyValid, Is.True, string.Join(" | ", report.Issues.Select(item => item.Message)));
            Assert.That(report.IsPublicVersionAligned, Is.False);
            Assert.That(report.PublicLanguageVersion, Is.EqualTo("0.3"));
        });
    }
}
