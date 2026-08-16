using FluNET.Language;

namespace FluNET.Tests;

public class LanguageBuildTests
{
    [Fact]
    public void Registry_build_returns_snapshot_and_language_diagnostics()
    {
        LanguageBuildResult result = new LanguageRegistry().Build();
        Assert.NotNull(result.Snapshot);
        Assert.NotNull(result.Diagnostics);
    }
}
