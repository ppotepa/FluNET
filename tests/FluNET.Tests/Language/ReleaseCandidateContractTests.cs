using FluNET.Language;
using FluNET.Language.Contracts;

namespace FluNET.Tests.Language;

[TestFixture]
public sealed class ReleaseCandidateContractTests
{
    [Test]
    public void CandidateContractsAreStaticallyConsistentButPublicVersionIsNotYetPromoted()
    {
        ReleaseContractReport report = ReleaseCandidateVerifier.Verify1_0(SurfaceLanguage.CreateRuntime().Language);
        Assert.Multiple(() =>
        {
            Assert.That(report.Issues, Is.Empty, string.Join(" | ", report.Issues.Select(issue => $"{issue.Code}:{issue.Message}")));
            Assert.That(report.CandidateVersion, Is.EqualTo("1.0"));
            Assert.That(report.PublicLanguageVersion, Is.EqualTo("0.3"));
            Assert.That(report.IsPublicVersionAligned, Is.False);
        });
    }
}
