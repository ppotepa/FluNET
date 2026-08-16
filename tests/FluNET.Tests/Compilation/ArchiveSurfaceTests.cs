using System.Text.Json;
using FluNET.Context;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class ArchiveSurfaceTests
{
    [Test]
    public void ListArchiveLowersToArchiveListingCommand()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();

        var compilation = context.CompileSurface(
            "LIST ARCHIVE \"./bundle.zip\" AS entries");

        Assert.That(compilation.IsValid, Is.True,
            string.Join(" | ", compilation.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        Assert.That(compilation.Lowering.CanonicalSyntax.Commands.Select(c => c.AllTokens.First().Text),
            Is.EqualTo(new[] { "LISTARCHIVE" }));
    }

    [Test]
    public async Task ListArchiveCanFeedTheJsonPipeline()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-archive-surface-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string source = Path.Combine(root, "input.txt");
        string zip = Path.Combine(root, "bundle.zip");
        try
        {
            await File.WriteAllTextAsync(source, "hello archive");
            FluNET.Capabilities.ZipFluNetArchive archive = new(new FluNET.Capabilities.AllowAllExecutionPolicy());
            await archive.CreateAsync(source, zip);

            using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
            var execution = await context.ExecuteSurfaceAsync(
                $"LIST ARCHIVE \"{zip}\" WHERE isDirectory == false AS entries");

            Assert.That(execution.Compilation.IsValid, Is.True,
                string.Join(" | ", execution.Compilation.Diagnostics.Select(d => d.Code + ":" + d.Message)));
            Assert.That(execution.Error, Is.Null, execution.Error?.ToString() ?? "execution failed");
            var entries = (JsonElement[])execution.Result!;
            Assert.That(entries, Has.Length.EqualTo(1));
            Assert.That(entries[0].GetProperty("path").GetString(), Is.EqualTo("input.txt"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
