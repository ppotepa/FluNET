using System.Text.Json;
using FluNET.Compilation;
using FluNET.Context;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class StructuredResourceSurfaceTests
{
    [Test]
    public async Task CompactLoadDecodesCsvIntoJsonRows()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-csv-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "people.csv");
        try
        {
            await File.WriteAllTextAsync(path, "id,name\n1,Ada\n2,Linus\n");
            using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
            SurfaceCompilationResult compilation = context.CompileSurface($"LOAD \"{path}\" AS people");
            SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync($"LOAD \"{path}\" AS people");

            Assert.That(compilation.IsValid, Is.True,
                string.Join(" | ", compilation.Diagnostics.Select(item => item.Message)));
            Assert.That(compilation.Plan!.Steps[0].Command.Frame.Id.Value, Is.EqualTo("surface.load.csv"));
            Assert.That(execution.IsSuccess, Is.True, execution.Error?.ToString());
            JsonElement[] rows = (JsonElement[])execution.Result!;
            Assert.That(rows[0].GetProperty("name").GetString(), Is.EqualTo("Ada"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task CompactLoadDecodesXmlIntoJson()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-xml-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "settings.xml");
        try
        {
            await File.WriteAllTextAsync(path, "<settings><mode>safe</mode></settings>");
            using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
            SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync($"LOAD \"{path}\" AS settings");

            Assert.That(execution.IsSuccess, Is.True, execution.Error?.ToString());
            Assert.That(execution.Result, Is.TypeOf<JsonElement>());
            Assert.That(((JsonElement)execution.Result!).GetProperty("settings").GetProperty("mode").GetProperty("#text").GetString(), Is.EqualTo("safe"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
