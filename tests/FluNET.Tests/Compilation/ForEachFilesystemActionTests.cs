using FluNET.Compilation;
using FluNET.Context;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class ForEachFilesystemActionTests
{
    [Test]
    public void ForEachAcceptsThePortableFilesystemActionSet()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult compilation = context.CompileSurface("""
GET https://example.test/items AS items
FOR EACH item IN items
    MKDIR "./out"
    COPY "./source.txt" TO "./out/source.txt"
    MOVE "./out/source.txt" TO "./out/processed.txt"
    PACK "./out" TO "./out.zip"
    UNPACK "./out.zip" TO "./restored"
    TRASH "./restored/processed.txt"
    PUBLISH "processed" TO "items"
    NOTIFY "item processed"
""");

        Assert.That(compilation.IsValid, Is.True,
            string.Join(" | ", compilation.Diagnostics.Select(item => item.Message)));
    }

    [Test]
    public async Task ForEachCanCreatePortableDirectoriesInItsIsolatedBody()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-foreach-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string data = Path.Combine(root, "items.csv");
        string target = Path.Combine(root, "created", "nested");
        try
        {
            await File.WriteAllTextAsync(data, "id\n1\n");
            using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
            SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync($"""
LOAD "{data}" AS items
FOR EACH item IN items
    MKDIR "{target}"
""");

            Assert.That(execution.IsSuccess, Is.True,
                execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Diagnostics.Select(item => item.Message)));
            Assert.That(Directory.Exists(target), Is.True);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
