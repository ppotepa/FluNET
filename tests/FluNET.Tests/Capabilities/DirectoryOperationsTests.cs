using FluNET.Capabilities;

namespace FluNET.Tests.Capabilities;

[TestFixture]
public sealed class DirectoryOperationsTests
{
    [Test]
    public async Task CreateDirectoryIsIdempotentAndPortable()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-directory-" + Guid.NewGuid().ToString("N"));
        try
        {
            PhysicalFluNetDirectoryOperations directories =
                new(new AllowAllExecutionPolicy());
            DirectoryInfo first = await directories.CreateAsync(Path.Combine(root, "nested", "reports"));
            DirectoryInfo second = await directories.CreateAsync(first.FullName);

            Assert.That(first.Exists, Is.True);
            Assert.That(second.FullName, Is.EqualTo(first.FullName));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task ListReturnsFilesAndDirectoriesWithMetadata()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-list-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "nested"));
        string file = Path.Combine(root, "report.txt");
        await File.WriteAllTextAsync(file, "report");
        try
        {
            PhysicalFluNetDirectoryOperations directories =
                new(new AllowAllExecutionPolicy());

            IReadOnlyList<FluNetDirectoryEntry> entries = await directories.ListAsync(root);

            Assert.That(entries.Select(entry => entry.Name), Does.Contain("nested"));
            FluNetDirectoryEntry report = entries.Single(entry => entry.Name == "report.txt");
            Assert.That(report.IsDirectory, Is.False);
            Assert.That(report.Length, Is.EqualTo(6));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task RecursiveListReturnsNestedEntries()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-list-recursive-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "nested", "deeper"));
        await File.WriteAllTextAsync(Path.Combine(root, "nested", "deeper", "report.txt"), "report");
        try
        {
            PhysicalFluNetDirectoryOperations directories = new(new AllowAllExecutionPolicy());

            IReadOnlyList<FluNetDirectoryEntry> entries = await directories.ListAsync(root, recursive: true);

            Assert.That(entries.Select(entry => entry.Name), Does.Contain("report.txt"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
