using FluNET.Language;

namespace FluNET.Tests;

public class LanguageIntrospectionTests
{
    [Test]
    public void Manifest_contains_compiled_get_metadata()
    {
        LanguageSnapshot snapshot = new LanguageRegistry().Snapshot;
        string json = LanguageIntrospection.ToJson(snapshot);
        Assert.That(json, Does.Contain("GET"));
        Assert.That(json, Does.Contain("patterns"));
        Assert.That(json, Does.Contain("resultType"));
    }

    [Test]
    public void Language_validator_accepts_standard_snapshot_without_missing_module_dependencies()
    {
        LanguageSnapshot snapshot = new LanguageRegistry().Snapshot;
        Assert.That(LanguageValidator.Validate(snapshot).Any(x => x.Code == "FLU-LANG-010"), Is.False);
    }
}
