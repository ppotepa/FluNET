using FluNET.Context;
using FluNET.Language.Resources;
using System.Text.Json;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class StructuredResourceDecoderTests
{
    [Test]
    public void CsvDecoderProducesJsonRowsWithScalarValues()
    {
        CsvResourceDecoder decoder = new();
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        ResourceDescriptor descriptor = new(new FileResourceReference("users.csv", true), ResourceFormat.Csv, context.GetService<FluNET.Language.LanguageSnapshot>().Types.List(context.GetService<FluNET.Language.LanguageSnapshot>().Types.Json), "users");
        JsonElement[] rows = (JsonElement[])decoder.Decode(descriptor, ResourcePayload.FromText("id,name,active\n1,Ada,true\n2,Linus,false", "text/csv"));
        Assert.Multiple(() => { Assert.That(rows, Has.Length.EqualTo(2)); Assert.That(rows[0].GetProperty("id").GetDecimal(), Is.EqualTo(1m)); Assert.That(rows[0].GetProperty("name").GetString(), Is.EqualTo("Ada")); Assert.That(rows[0].GetProperty("active").GetBoolean(), Is.True); });
    }

    [Test]
    public void XmlDecoderProducesDeterministicJsonTree()
    {
        XmlResourceDecoder decoder = new();
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        ResourceDescriptor descriptor = new(new FileResourceReference("config.xml", true), ResourceFormat.Xml, context.GetService<FluNET.Language.LanguageSnapshot>().Types.Json, "config");
        JsonElement json = (JsonElement)decoder.Decode(descriptor, ResourcePayload.FromText("<config env=\"prod\"><port>8080</port></config>", "application/xml"));
        JsonElement config = json.GetProperty("config");
        Assert.Multiple(() => { Assert.That(config.GetProperty("@env").GetString(), Is.EqualTo("prod")); Assert.That(config.GetProperty("port").GetProperty("#text").GetDecimal(), Is.EqualTo(8080m)); });
    }
}
