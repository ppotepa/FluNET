using FluNET.Context;
using System.Text.Json;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class FileQuerySurfaceTests
{
    [Test]
    public void FindWhereLowersToScanAndFilter()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();

        var compilation = context.CompileSurface(
            "FIND \"./incoming\" WHERE extension == '.json' AS files");

        Assert.That(compilation.IsValid, Is.True,
            string.Join(" | ", compilation.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        Assert.That(compilation.Lowering.CanonicalSyntax.Commands.Select(c => c.AllTokens.First().Text),
            Is.EqualTo(new[] { "SCANFILES", "FILTERJSON" }));
    }

    [Test]
    public async Task FindWhereFiltersFilesystemMetadata()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-query-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "keep.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(root, "skip.txt"), "text");

        try
        {
            using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
            string source = $"FIND \"{root}\" WHERE extension == '.json' AS files";
            var execution = await context.ExecuteSurfaceAsync(source);

            Assert.That(execution.IsSuccess, Is.True,
                execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Diagnostics.Select(d => d.Message)));
            var files = (System.Text.Json.JsonElement[])execution.Result!;
            Assert.That(files, Has.Length.EqualTo(1));
            Assert.That(files[0].GetProperty("extension").GetString(), Is.EqualTo(".json"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task FindExposesPortableNameAndVisibilityMetadata()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-query-metadata-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "visible.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(root, ".hidden.json"), "{}");

        try
        {
            using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
            var execution = await context.ExecuteSurfaceAsync(
                $"FIND \"{root}\" WHERE isHidden == false AND nameWithoutExtension == 'visible' AS files");

            Assert.That(execution.IsSuccess, Is.True,
                execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Diagnostics.Select(d => d.Message)));
            var files = (System.Text.Json.JsonElement[])execution.Result!;
            Assert.That(files, Has.Length.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(files[0].GetProperty("isHidden").GetBoolean(), Is.False);
                Assert.That(files[0].GetProperty("nameWithoutExtension").GetString(), Is.EqualTo("visible"));
                Assert.That(files[0].GetProperty("relativePath").GetString(), Is.EqualTo("visible.json"));
                Assert.That(files[0].GetProperty("directory").GetString(), Is.EqualTo(Path.GetFullPath(root)));
            });
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task FindSupportsReadableTextAndGlobPredicates()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-query-predicates-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "report-final.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(root, "notes.txt"), "text");

        try
        {
            using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
            var execution = await context.ExecuteSurfaceAsync(
                $"FIND \"{root}\" WHERE name CONTAINS 'report' AND name MATCHES '*.json' AS files");

            Assert.That(execution.IsSuccess, Is.True,
                execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Diagnostics.Select(d => d.Message)));
            var files = (System.Text.Json.JsonElement[])execution.Result!;
            Assert.That(files, Has.Length.EqualTo(1));
            Assert.That(files[0].GetProperty("name").GetString(), Is.EqualTo("report-final.json"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task FindSupportsPortableMetadataAliasesAndNaturalTimePredicates()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-query-aliases-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string large = Path.Combine(root, "large.json");
        string small = Path.Combine(root, "small.json");
        await File.WriteAllTextAsync(large, new string('x', 32));
        await File.WriteAllTextAsync(small, "{}");

        try
        {
            using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
            SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(
                $"FIND \"{root}\" WHERE size > 10 AND modified AFTER '2000-01-01T00:00:00.0000000Z' AS files");

            Assert.That(execution.IsSuccess, Is.True,
                execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Diagnostics.Select(d => d.Message)));
            JsonElement[] files = (JsonElement[])execution.Result!;
            Assert.That(files.Select(file => file.GetProperty("name").GetString()), Is.EqualTo(new[] { "large.json" }));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task FindLimitBoundsProviderEnumeration()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-query-limit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            for (int index = 0; index < 5; index++)
                await File.WriteAllTextAsync(Path.Combine(root, $"file-{index}.txt"), index.ToString());

            using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
            SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync($"FIND \"{root}\" LIMIT 2 AS files");

            Assert.That(execution.IsSuccess, Is.True, execution.Error?.ToString());
            Assert.That((System.Text.Json.JsonElement[])execution.Result!, Has.Length.EqualTo(2));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task FindSupportsOrderingAndPagingClauses()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-query-order-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "small.txt"), "1");
            await File.WriteAllTextAsync(Path.Combine(root, "large.txt"), new string('x', 32));
            await File.WriteAllTextAsync(Path.Combine(root, "middle.txt"), new string('x', 12));

            using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
            SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(
                $"FIND \"{root}\" ORDER BY size DESC SKIP 1 TAKE 1 AS files");

            Assert.That(execution.IsSuccess, Is.True, execution.Error?.ToString());
            System.Text.Json.JsonElement[] files = (System.Text.Json.JsonElement[])execution.Result!;
            Assert.That(files.Single().GetProperty("name").GetString(), Is.EqualTo("middle.txt"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
