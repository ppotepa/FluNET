using FluNET.Capabilities;

namespace FluNET.Tests.Capabilities;

[TestFixture]
public sealed class ArchiveTests
{
    [Test]
    public async Task PackingADirectoryAgainReplacesTheExistingArchive()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-archive-replace-" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(root, "source");
        string zip = Path.Combine(root, "bundle.zip");
        Directory.CreateDirectory(source);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(source, "first.txt"), "first");
            ZipFluNetArchive archive = new(new AllowAllExecutionPolicy());
            await archive.CreateAsync(source, zip);
            await File.WriteAllTextAsync(Path.Combine(source, "second.txt"), "second");

            await archive.CreateAsync(source, zip);

            IReadOnlyList<FluNetArchiveEntry> entries = await archive.ListAsync(zip);
            Assert.That(entries.Select(item => item.Path),
                Is.EquivalentTo(new[] { "first.txt", "second.txt" }));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task ZipArchiveRoundTripsAFilePortably()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-archive-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string source = Path.Combine(root, "input.txt");
        string zip = Path.Combine(root, "bundle.zip");
        string output = Path.Combine(root, "output");
        try
        {
            await File.WriteAllTextAsync(source, "hello archive");
            ZipFluNetArchive archive = new(new AllowAllExecutionPolicy());
            await archive.CreateAsync(source, zip);
            await archive.ExtractAsync(zip, output);

            Assert.That(await File.ReadAllTextAsync(Path.Combine(output, "input.txt")),
                Is.EqualTo("hello archive"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task ZipArchiveListsPortableEntryMetadata()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-archive-list-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string source = Path.Combine(root, "input.txt");
        string zip = Path.Combine(root, "bundle.zip");
        try
        {
            await File.WriteAllTextAsync(source, "hello archive");
            ZipFluNetArchive archive = new(new AllowAllExecutionPolicy());
            await archive.CreateAsync(source, zip);

            IReadOnlyList<FluNetArchiveEntry> entries = await archive.ListAsync(zip);
            Assert.That(entries, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(entries[0].Path, Is.EqualTo("input.txt"));
                Assert.That(entries[0].Length, Is.EqualTo(13));
                Assert.That(entries[0].CompressedLength, Is.GreaterThan(0));
                Assert.That(entries[0].IsDirectory, Is.False);
            });
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task TarArchiveRoundTripsAFilePortably()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-tar-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string source = Path.Combine(root, "input.txt");
        string tar = Path.Combine(root, "bundle.tar");
        string output = Path.Combine(root, "output");
        try
        {
            await File.WriteAllTextAsync(source, "hello tar");
            PortableFluNetArchive archive = new(new AllowAllExecutionPolicy());
            await archive.CreateAsync(source, tar);
            IReadOnlyList<FluNetArchiveEntry> entries = await archive.ListAsync(tar);
            await archive.ExtractAsync(tar, output);

            Assert.That(entries.Select(entry => entry.Path), Is.EqualTo(new[] { "input.txt" }));
            Assert.That(await File.ReadAllTextAsync(Path.Combine(output, "input.txt")), Is.EqualTo("hello tar"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task GzipTarArchiveRoundTripsAFilePortably()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-targz-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string source = Path.Combine(root, "input.txt");
        string archivePath = Path.Combine(root, "bundle.tar.gz");
        string output = Path.Combine(root, "output");
        try
        {
            await File.WriteAllTextAsync(source, "hello tar gzip");
            PortableFluNetArchive archive = new(new AllowAllExecutionPolicy());
            await archive.CreateAsync(source, archivePath);
            IReadOnlyList<FluNetArchiveEntry> entries = await archive.ListAsync(archivePath);
            await archive.ExtractAsync(archivePath, output);

            Assert.That(entries.Select(entry => entry.Path), Is.EqualTo(new[] { "input.txt" }));
            Assert.That(await File.ReadAllTextAsync(Path.Combine(output, "input.txt")), Is.EqualTo("hello tar gzip"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
