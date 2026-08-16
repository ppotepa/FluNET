using System.Text.Json;
using FluNET.Context;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class DirectoryQuerySurfaceTests
{
    [Test]
    public void ListQueryLowersToDirectoryAndCollectionStages()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();

        var compilation = context.CompileSurface(
            "LIST \"./data\" WHERE isDirectory == false ORDER BY length DESC TAKE 5 AS entries");

        Assert.That(compilation.IsValid, Is.True,
            string.Join(" | ", compilation.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        Assert.That(compilation.Plan!.Steps.Select(step => step.Command.Frame.Id.Value),
            Is.EqualTo(new[]
            {
                "surface.files.list.json",
                "surface.data.filter.json",
                "surface.data.sort.json",
                "surface.data.take.json"
            }));
    }

    [Test]
    public async Task ListQueryFiltersSortsAndTakesEntries()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-list-query-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "small.txt"), "1");
        await File.WriteAllTextAsync(Path.Combine(root, "large.json"), "123456789");
        Directory.CreateDirectory(Path.Combine(root, "folder.json"));

        try
        {
            using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
            var execution = await context.ExecuteSurfaceAsync(
                $"LIST \"{root}\" WHERE isDirectory == false ORDER BY length DESC TAKE 1 AS entries");

            Assert.That(execution.IsSuccess, Is.True,
                execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Diagnostics.Select(d => d.Message)));
            JsonElement[] entries = (JsonElement[])execution.Result!;
            Assert.That(entries, Has.Length.EqualTo(1));
            Assert.That(entries[0].GetProperty("name").GetString(), Is.EqualTo("large.json"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
