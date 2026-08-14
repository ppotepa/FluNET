using FluNET.Declarative.Reconciliation;
using System.Text.Json;

namespace FluNET.Tests.Declarative;

[TestFixture]
public sealed class ReconciliationStateTests
{
    [Test]
    public void FingerprintIgnoresObjectPropertyOrder()
    {
        using JsonDocument first = JsonDocument.Parse("{\"id\":1,\"name\":\"Ada\"}");
        using JsonDocument second = JsonDocument.Parse("{\"name\":\"Ada\",\"id\":1}");
        Assert.That(StateCanonicalizer.Fingerprint(first.RootElement), Is.EqualTo(StateCanonicalizer.Fingerprint(second.RootElement)));
    }

    [Test]
    public void SnapshotRejectsDuplicateIdentityKeys()
    {
        using JsonDocument first = JsonDocument.Parse("{\"id\":1}");
        using JsonDocument second = JsonDocument.Parse("{\"id\":1,\"name\":\"duplicate\"}");
        Assert.That(() => new DesiredStateSnapshot(ResourceIdentity.Parse("file:desired.json"), "id", new[] { first.RootElement, second.RootElement }), Throws.TypeOf<FormatException>());
    }
}
