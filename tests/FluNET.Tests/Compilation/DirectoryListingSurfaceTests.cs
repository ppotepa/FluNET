using FluNET.Compilation;
using FluNET.Context;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class DirectoryListingSurfaceTests
{
    [Test]
    public void ListCompilesToTheDirectoryCapability()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();

        SurfaceCompilationResult compilation = context.CompileSurface("LIST \"./\" AS entries");

        Assert.That(compilation.IsValid, Is.True,
            string.Join(" | ", compilation.Lowering.Diagnostics.Select(item => item.Code + ": " + item.Message)
                .Concat(compilation.Diagnostics.Select(item => item.Code + ": " + item.Message))));
        Assert.That(compilation.Plan!.Steps.Single().Command.Frame.Id.Value,
            Is.EqualTo("surface.files.list.json"));
    }

    [Test]
    public async Task ListExecutesAgainstThePortableDirectoryProvider()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-list-surface-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "nested"));
        await File.WriteAllTextAsync(Path.Combine(root, "report.txt"), "report");
        await File.WriteAllTextAsync(Path.Combine(root, ".hidden"), "hidden");
        try
        {
            using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
            SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync($"LIST \"{root}\" AS entries");

            Assert.That(execution.IsSuccess, Is.True,
                execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Lowering.Diagnostics.Select(item => item.Code + ": " + item.Message)
                    .Concat(execution.Compilation.Diagnostics.Select(item => item.Code + ": " + item.Message))));
            System.Text.Json.JsonElement[] entries = (System.Text.Json.JsonElement[])execution.Result!;
            Assert.That(entries.Select(entry => entry.GetProperty("name").GetString()), Does.Contain("report.txt"));
            Assert.That(entries.Select(entry => entry.GetProperty("name").GetString()), Does.Contain("nested"));
            Assert.That(entries.Single(entry => entry.GetProperty("name").GetString() == ".hidden").GetProperty("isHidden").GetBoolean(), Is.True);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task RecursiveListIncludesNestedEntries()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-list-recursive-surface-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "nested"));
        await File.WriteAllTextAsync(Path.Combine(root, "nested", "report.txt"), "report");
        try
        {
            using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
            SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync($"LIST \"{root}\" RECURSIVE AS entries");

            Assert.That(execution.IsSuccess, Is.True,
                execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Diagnostics.Select(item => item.Message)));
            System.Text.Json.JsonElement[] entries = (System.Text.Json.JsonElement[])execution.Result!;
            Assert.That(entries.Select(entry => entry.GetProperty("name").GetString()), Does.Contain("report.txt"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task StatReturnsPortableMetadataForExistingAndMissingPaths()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-stat-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string file = Path.Combine(root, "report.txt");
        await File.WriteAllTextAsync(file, "report");
        try
        {
            using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
            SurfaceExecutionResult existing = await context.ExecuteSurfaceAsync($"STAT \"{file}\" AS info");
            SurfaceExecutionResult missing = await context.ExecuteSurfaceAsync($"STAT \"{Path.Combine(root, "missing.txt")}\" AS info");

            Assert.That(existing.IsSuccess, Is.True, existing.Error?.ToString());
            Assert.That(missing.IsSuccess, Is.True, missing.Error?.ToString());
            Assert.That(((System.Text.Json.JsonElement)existing.Result!).GetProperty("exists").GetBoolean(), Is.True);
            Assert.That(((System.Text.Json.JsonElement)existing.Result!).GetProperty("length").GetInt64(), Is.EqualTo(6));
            Assert.That(((System.Text.Json.JsonElement)missing.Result!).GetProperty("exists").GetBoolean(), Is.False);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
