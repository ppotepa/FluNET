using FluNET.Language;

namespace FluNET.Tests;

public class LanguageIntrospectionTests
{
    [Fact]
    public void Manifest_contains_compiled_get_metadata()
    {
        LanguageSnapshot snapshot = new LanguageRegistry().Snapshot;
        string json = LanguageIntrospection.ToJson(snapshot);

        Assert.Contains("GET", json);
        Assert.Contains("clauses", json);
        Assert.Contains("resultType", json);
    }

    [Fact]
    public void Language_validator_accepts_standard_snapshot_without_missing_module_dependencies()
    {
        LanguageSnapshot snapshot = new LanguageRegistry().Snapshot;
        Assert.DoesNotContain(LanguageValidator.Validate(snapshot), x => x.Code == "FLU-LANG-010");
    }
}
