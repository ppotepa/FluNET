using FluNET.Language;

namespace FluNET.Tests;

public class MetadataConventionInferenceTests
{
    [Test]
    public void Standard_file_get_infers_read_capability_and_idempotent_traits()
    {
        VerbDescriptor get = new LanguageRegistry().Snapshot.GetVerbOverloads("GET")
            .First(x => x.Patterns.SelectMany(p => p.Pattern.Clauses).Any(c => c.ValueType == typeof(FileInfo)));

        Assert.That(get.Capabilities, Does.Contain("filesystem.read"));
        Assert.That(get.Traits.Idempotent, Is.True);
        Assert.That(get.Traits.Retryable, Is.True);
    }
}
