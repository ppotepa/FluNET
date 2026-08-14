using FluNET.Compilation.Inference;
using FluNET.Language;
using FluNET.Language.Resources;
using FluNET.Prompt.Surface;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class ResourceInferenceTests
{
    [TestCase("post.json", ResourceKind.LocalFile, ResourceFormat.Json, "Json", "post")]
    [TestCase("template.txt", ResourceKind.LocalFile, ResourceFormat.Text, "Text", "template")]
    [TestCase("https://example.test/api/users.json", ResourceKind.Http, ResourceFormat.Json, "Json", "users")]
    [TestCase("env:DATABASE_URL", ResourceKind.Environment, ResourceFormat.Text, "Text", "database_url")]
    [TestCase("secret:github-token", ResourceKind.Secret, ResourceFormat.Text, "Text", "github_token")]
    public void ResourceInferenceIsDeterministic(
        string source,
        ResourceKind kind,
        ResourceFormat format,
        string typeName,
        string variable)
    {
        LanguageSnapshot language = StandardLanguage.CreateSnapshot();
        SurfaceValueSyntax value = new(source, new FluNET.Prompt.SourceSpan(0, source.Length));
        InferenceTrace trace = new();

        ResourceDescriptor descriptor = new InferenceEngine().InferResource(value, language, trace);

        Assert.Multiple(() =>
        {
            Assert.That(descriptor.Reference.Kind, Is.EqualTo(kind));
            Assert.That(descriptor.Format, Is.EqualTo(format));
            Assert.That(descriptor.Type.Name, Is.EqualTo(typeName));
            Assert.That(descriptor.SuggestedVariableName, Is.EqualTo(variable));
            Assert.That(trace.Items, Has.Count.EqualTo(4));
        });
    }
}
