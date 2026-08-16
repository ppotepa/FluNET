using FluNET.Context;
using FluNET.Tooling;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class DirectoryTransferSurfaceTests
{
    [TestCase("COPY DIRECTORY \"./source\" TO \"./target\" AS copied", "filesystem.directory.copy")]
    [TestCase("MOVE DIRECTORY \"./source\" TO \"./target\" AS moved", "filesystem.directory.move")]
    [TestCase("TRASH DIRECTORY \"./source\" AS removed", "filesystem.directory.trash")]
    [TestCase("RESTORE DIRECTORY \"./.flunet-trash/item\" TO \"./restored\" AS restored", "filesystem.trash.restore.directory")]
    public void DirectoryTransferSyntaxLowersToDirectoryFrame(string source, string frame)
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var compilation = context.CompileSurface(source);

        Assert.That(compilation.IsValid, Is.True,
            string.Join(" | ", compilation.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        Assert.That(compilation.BoundProgram!.Commands.Single().Frame.Id.Value, Is.EqualTo(frame));
    }

    [Test]
    public async Task CopyDirectoryTransfersNestedFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-directory-transfer-" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "source");
        string target = Path.Combine(root, "target");
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        await File.WriteAllTextAsync(Path.Combine(source, "nested", "value.txt"), "hello");

        try
        {
            using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
            var execution = await context.ExecuteSurfaceAsync($"COPY DIRECTORY \"{source}\" TO \"{target}\" AS copied");

            Assert.That(execution.IsSuccess, Is.True,
                execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Lowering.Diagnostics.Select(d => d.Code + ":" + d.Message)));
            Assert.That(File.ReadAllText(Path.Combine(target, "nested", "value.txt")), Is.EqualTo("hello"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task TrashDirectoryMovesTreeToRecoverableTrash()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-directory-trash-" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "source");
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        await File.WriteAllTextAsync(Path.Combine(source, "nested", "value.txt"), "hello");

        try
        {
            using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
            var execution = await context.ExecuteSurfaceAsync($"TRASH DIRECTORY \"{source}\" AS removed");

            Assert.That(execution.IsSuccess, Is.True,
                execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Lowering.Diagnostics.Select(d => d.Code + ":" + d.Message)));
            Assert.That(Directory.Exists(source), Is.False);
            Assert.That(Directory.GetDirectories(root, ".flunet-trash").Single(), Does.Exist);
            Assert.That(Directory.EnumerateFiles(Path.Combine(root, ".flunet-trash"), "*", SearchOption.AllDirectories).Single(), Does.Exist);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task FileTrashCanBeRestoredThroughTheSameWorkflow()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-restore-" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "source.txt");
        string target = Path.Combine(root, "restored.txt");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(source, "recover me");

        try
        {
            using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
            var execution = await context.ExecuteSurfaceAsync($"TRASH \"{source}\" AS removed\nRESTORE [removed] TO \"{target}\" AS restored");

            Assert.That(execution.IsSuccess, Is.True,
                execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Lowering.Diagnostics.Select(d => d.Code + ":" + d.Message)));
            Assert.That(File.Exists(source), Is.False);
            Assert.That(await File.ReadAllTextAsync(target), Is.EqualTo("recover me"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
