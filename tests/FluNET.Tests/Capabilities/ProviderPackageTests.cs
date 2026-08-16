using FluNET.Capabilities;
using NUnit.Framework;

namespace FluNET.Tests.Capabilities;

public sealed class ProviderPackageTests
{
    [Test]
    public void CatalogValidatesNormalizesAndFiltersByPlatform()
    {
        InMemoryFluNetProviderPackageCatalog catalog = new();
        catalog.Register(new FluNetProviderPackageManifest(
            "Demo.Storage", " 1.2 ", "Demo.Entry", [FluNetPlatform.Any], ["Storage.BLOB", "storage.blob"], ["storage.read"]));

        FluNetProviderPackageManifest package = catalog.Discover(true).Single();
        Assert.That(package.Id, Is.EqualTo("demo.storage"));
        Assert.That(package.Version, Is.EqualTo("1.2"));
        Assert.That(package.Capabilities, Is.EqualTo(new[] { "storage.blob" }));
    }

    [Test]
    public void JsonCatalogPersistsValidatedManifest()
    {
        string directory = Path.Combine(Path.GetTempPath(), "flunet-packages-" + Guid.NewGuid().ToString("N"));
        try
        {
            JsonFileFluNetProviderPackageCatalog catalog = new(directory, new AllowAllExecutionPolicy());
            catalog.Register(new FluNetProviderPackageManifest("demo", "1.0", "Demo.Entry", [FluNetPlatform.Any], ["demo.cap"], []));
            Assert.That(catalog.Discover(), Has.Count.EqualTo(1));
            Assert.That(File.Exists(Path.Combine(directory, "demo.json")), Is.True);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
