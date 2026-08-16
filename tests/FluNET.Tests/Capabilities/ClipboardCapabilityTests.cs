using FluNET.Capabilities;

namespace FluNET.Tests.Capabilities;

[TestFixture]
public sealed class ClipboardCapabilityTests
{
    [Test]
    public void DenyClipboardFailsExplicitly()
    {
        DenyFluNetClipboard clipboard = new();

        Assert.ThrowsAsync<CapabilityDeniedException>(() => clipboard.ReadTextAsync().AsTask());
    }

    [Test]
    public void ClipboardProviderDescribesReadPermission()
    {
        ClipboardCapabilityProvider provider = new(new DenyFluNetClipboard());

        Assert.Multiple(() =>
        {
        Assert.That(provider.Descriptor.Id, Is.EqualTo("system.clipboard"));
        Assert.That(provider.Descriptor.Permissions, Does.Contain("system.clipboard.read"));
        Assert.That(provider.Descriptor.Permissions, Does.Contain("system.clipboard.write"));
            Assert.That(provider.IsAvailable, Is.False);
        });
    }
}
