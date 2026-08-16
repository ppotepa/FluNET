using System.Text.Json;
using FluNET.Context;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class FileSearchSurfaceTests
{
    [Test]
    public async Task SearchFindsTextAndSupportsRecursiveRegexModes()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-search-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "nested"));
        await File.WriteAllTextAsync(Path.Combine(root, "one.txt"), "hello world\nother");
        await File.WriteAllTextAsync(Path.Combine(root, "nested", "two.txt"), "HELLO 42\nnone");
        await File.WriteAllBytesAsync(Path.Combine(root, "nested", "binary.bin"),
            [0, 1, 2, 104, 101, 108, 108, 111]);
        try
        {
            using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
            var execution = await context.ExecuteSurfaceAsync(
                $"SEARCH REGEX \"hello [0-9]+\" IN \"{root}\" RECURSIVE AS matches");

            Assert.That(execution.IsSuccess, Is.True,
                execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Diagnostics.Select(d => d.Message)));
            JsonElement[] matches = (JsonElement[])execution.Result!;
            Assert.That(matches, Has.Length.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(matches[0].GetProperty("line").GetInt32(), Is.EqualTo(1));
                Assert.That(matches[0].GetProperty("column").GetInt32(), Is.EqualTo(1));
            Assert.That(matches[0].GetProperty("text").GetString(), Is.EqualTo("HELLO 42"));
            });
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void SearchLowersToFilesystemCapabilityFrame()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var result = context.CompileSurface("SEARCH \"needle\" IN \"./docs\" AS matches");

        Assert.That(result.IsValid, Is.True,
            string.Join(" | ", result.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        Assert.That(result.Lowering.CanonicalSyntax.Commands.Single().AllTokens.First().Text,
            Is.EqualTo("SEARCHFILES"));
        Assert.That(result.Plan!.Steps.Single().Command.Frame.Id.Value,
            Is.EqualTo("filesystem.search"));
    }

    [Test]
    public async Task SearchStopsAtTheRequestedLimit()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-search-limit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "many.txt"), "hit\nhit\nhit\n");
        try
        {
            using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
            var execution = await context.ExecuteSurfaceAsync(
                $"SEARCH \"hit\" IN \"{root}\" LIMIT 2 AS matches");

            Assert.That(execution.IsSuccess, Is.True,
                execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Diagnostics.Select(d => d.Message)));
            Assert.That((JsonElement[])execution.Result!, Has.Length.EqualTo(2));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
