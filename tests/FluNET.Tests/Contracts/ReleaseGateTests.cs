using FluNET.Language.Contracts;

namespace FluNET.Tests.Contracts;

[TestFixture]
public sealed class ReleaseGateTests
{
    [Test]
    public void NoBuildEvidenceCanNeverPromoteOrRelease()
    {
        ReleaseGateAssessment assessment = ReleasePromotionGate.Assess1_0(Array.Empty<ReleaseGateCheckResult>());
        Assert.Multiple(() =>
        {
            Assert.That(assessment.HasCompleteEvidence, Is.False);
            Assert.That(assessment.ReadyForVersionPromotion, Is.False);
            Assert.That(assessment.ReadyForRelease, Is.False);
            Assert.That(assessment.Blockers, Has.Count.GreaterThan(0));
        });
    }

    [Test]
    public void FullPrePromotionEvidenceAllowsOnlyVersionPromotionWhilePublicIdentityIsOld()
    {
        ReleaseGateCheckResult[] evidence = Enum.GetValues<ReleaseGateCheckId>()
            .Select(check => new ReleaseGateCheckResult(check, true, "verified by external gate"))
            .ToArray();

        ReleaseGateAssessment assessment = ReleasePromotionGate.Assess1_0(evidence, "0.3");

        Assert.Multiple(() =>
        {
            Assert.That(assessment.HasCompleteEvidence, Is.True);
            Assert.That(assessment.ReadyForVersionPromotion, Is.True);
            Assert.That(assessment.ReadyForRelease, Is.False);
        });
    }

    [Test]
    public void ReleaseRequiresCompleteEvidenceAfterPublicVersionIsAligned()
    {
        ReleaseGateCheckResult[] evidence = Enum.GetValues<ReleaseGateCheckId>()
            .Select(check => new ReleaseGateCheckResult(check, true))
            .ToArray();
        ReleaseGateAssessment assessment = ReleasePromotionGate.Assess1_0(evidence, "1.0");

        Assert.Multiple(() =>
        {
            Assert.That(assessment.ReadyForVersionPromotion, Is.False);
            Assert.That(assessment.ReadyForRelease, Is.True);
        });
    }
}
