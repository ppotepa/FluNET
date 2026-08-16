using FluNET.Language;

namespace FluNET.Tests;

public class LanguageBuildTests
{
    [Test]
    public void Registry_build_returns_snapshot_and_language_diagnostics()
    {
        LanguageBuildResult result = new LanguageRegistry().Build();
        Assert.That(result.Snapshot, Is.Not.Null);
        Assert.That(result.Diagnostics, Is.Not.Null);
    }
}
