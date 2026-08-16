using System.IO.Compression;
using System.Formats.Tar;

namespace FluNET.Capabilities;

public interface IFluNetArchive
{
    ValueTask<string> CreateAsync(
        string source,
        string destination,
        CancellationToken cancellationToken = default);

    ValueTask<string> ExtractAsync(
        string source,
        string destination,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<FluNetArchiveEntry>> ListAsync(
        string source,
        CancellationToken cancellationToken = default);
}

public sealed record FluNetArchiveEntry(
    string Path,
    long Length,
    long CompressedLength,
    DateTimeOffset? ModifiedUtc,
    bool IsDirectory);

public sealed class ZipFluNetArchive(IExecutionPolicy policy) : IFluNetArchive
{
    public async ValueTask<string> CreateAsync(
        string source,
        string destination,
        CancellationToken cancellationToken = default)
    {
        policy.EnsureFileAccess(source);
        policy.EnsureFileAccess(destination);
        cancellationToken.ThrowIfCancellationRequested();
        string fullSource = Path.GetFullPath(source);
        string fullDestination = Path.GetFullPath(destination);
        string? directory = Path.GetDirectoryName(fullDestination);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        if (!Directory.Exists(fullSource) && !File.Exists(fullSource))
            throw new FileNotFoundException($"Archive source '{fullSource}' was not found.", fullSource);
        if (PathEquals(fullSource, fullDestination))
            throw new IOException("Archive source and destination must be different paths.");
        if (File.Exists(fullDestination)) File.Delete(fullDestination);

        if (Directory.Exists(fullSource))
            ZipFile.CreateFromDirectory(fullSource, fullDestination, CompressionLevel.Fastest, includeBaseDirectory: false);
        else if (File.Exists(fullSource))
        {
            await using FileStream stream = File.Create(fullDestination);
            using ZipArchive archive = new(stream, ZipArchiveMode.Create);
            ZipArchiveEntry entry = archive.CreateEntry(Path.GetFileName(fullSource), CompressionLevel.Fastest);
            await using Stream target = entry.Open();
            await using FileStream input = File.OpenRead(fullSource);
            await input.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
        }
        return fullDestination;
    }

    public ValueTask<string> ExtractAsync(
        string source,
        string destination,
        CancellationToken cancellationToken = default)
    {
        policy.EnsureFileAccess(source);
        policy.EnsureFileAccess(destination);
        cancellationToken.ThrowIfCancellationRequested();
        string fullSource = Path.GetFullPath(source);
        string fullDestination = Path.GetFullPath(destination);
        if (!File.Exists(fullSource))
            throw new FileNotFoundException($"Archive '{fullSource}' was not found.", fullSource);
        Directory.CreateDirectory(fullDestination);
        ZipFile.ExtractToDirectory(fullSource, fullDestination, overwriteFiles: true);
        return ValueTask.FromResult(fullDestination);
    }

    public ValueTask<IReadOnlyList<FluNetArchiveEntry>> ListAsync(
        string source,
        CancellationToken cancellationToken = default)
    {
        policy.EnsureFileAccess(source);
        cancellationToken.ThrowIfCancellationRequested();
        string fullSource = Path.GetFullPath(source);
        if (!File.Exists(fullSource))
            throw new FileNotFoundException($"Archive '{fullSource}' was not found.", fullSource);

        using ZipArchive archive = ZipFile.OpenRead(fullSource);
        List<FluNetArchiveEntry> entries = archive.Entries
            .Select(entry => new FluNetArchiveEntry(
                entry.FullName,
                entry.Length,
                entry.CompressedLength,
                entry.LastWriteTime == default ? null : entry.LastWriteTime.ToUniversalTime(),
                entry.FullName.EndsWith("/", StringComparison.Ordinal)))
            .ToList();
        return ValueTask.FromResult<IReadOnlyList<FluNetArchiveEntry>>(entries);
    }

    private static bool PathEquals(string left, string right) => string.Equals(
        left,
        right,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}

public sealed class TarFluNetArchive(IExecutionPolicy policy, bool gzip = false) : IFluNetArchive
{
    public ValueTask<string> CreateAsync(string source, string destination, CancellationToken cancellationToken = default)
    {
        policy.EnsureFileAccess(source);
        policy.EnsureFileAccess(destination);
        cancellationToken.ThrowIfCancellationRequested();
        string fullSource = Path.GetFullPath(source);
        string fullDestination = Path.GetFullPath(destination);
        string? directory = Path.GetDirectoryName(fullDestination);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        if (!Directory.Exists(fullSource) && !File.Exists(fullSource))
            throw new FileNotFoundException($"Archive source '{fullSource}' was not found.", fullSource);
        if (PathEquals(fullSource, fullDestination))
            throw new IOException("Archive source and destination must be different paths.");
        if (File.Exists(fullDestination)) File.Delete(fullDestination);
        if (Directory.Exists(fullSource) && !gzip)
        {
            TarFile.CreateFromDirectory(fullSource, fullDestination, includeBaseDirectory: false);
        }
        else if (Directory.Exists(fullSource) || File.Exists(fullSource))
        {
            string tarPath = gzip ? TemporaryTarPath(fullDestination) : fullDestination;
            try
            {
                if (Directory.Exists(fullSource))
                {
                    TarFile.CreateFromDirectory(fullSource, tarPath, includeBaseDirectory: false);
                }
                else
                {
                    using FileStream output = File.Create(tarPath);
                    using TarWriter writer = new(output, TarEntryFormat.Pax, leaveOpen: false);
                    using FileStream input = File.OpenRead(fullSource);
                    writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, Path.GetFileName(fullSource))
                    {
                        DataStream = input
                    });
                }
                if (gzip) Compress(tarPath, fullDestination);
            }
            finally
            {
                if (gzip && File.Exists(tarPath)) File.Delete(tarPath);
            }
        }
        return ValueTask.FromResult(fullDestination);
    }

    public ValueTask<string> ExtractAsync(string source, string destination, CancellationToken cancellationToken = default)
    {
        policy.EnsureFileAccess(source);
        policy.EnsureFileAccess(destination);
        cancellationToken.ThrowIfCancellationRequested();
        string fullSource = Path.GetFullPath(source);
        string fullDestination = Path.GetFullPath(destination);
        if (!File.Exists(fullSource)) throw new FileNotFoundException($"Archive '{fullSource}' was not found.", fullSource);
        Directory.CreateDirectory(fullDestination);
        if (!gzip)
        {
            TarFile.ExtractToDirectory(fullSource, fullDestination, overwriteFiles: true);
        }
        else
        {
            string tarPath = TemporaryTarPath(fullDestination);
            try
            {
                Decompress(fullSource, tarPath);
                TarFile.ExtractToDirectory(tarPath, fullDestination, overwriteFiles: true);
            }
            finally
            {
                if (File.Exists(tarPath)) File.Delete(tarPath);
            }
        }
        return ValueTask.FromResult(fullDestination);
    }

    public ValueTask<IReadOnlyList<FluNetArchiveEntry>> ListAsync(string source, CancellationToken cancellationToken = default)
    {
        policy.EnsureFileAccess(source);
        cancellationToken.ThrowIfCancellationRequested();
        string fullSource = Path.GetFullPath(source);
        if (!File.Exists(fullSource)) throw new FileNotFoundException($"Archive '{fullSource}' was not found.", fullSource);
        List<FluNetArchiveEntry> entries = [];
        using FileStream file = File.OpenRead(fullSource);
        using Stream stream = gzip ? new GZipStream(file, CompressionMode.Decompress) : file;
        using TarReader reader = new(stream, leaveOpen: false);
        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            entries.Add(new FluNetArchiveEntry(
                entry.Name,
                entry.Length,
                entry.Length,
                entry.ModificationTime,
                entry.EntryType == TarEntryType.Directory));
        }
        return ValueTask.FromResult<IReadOnlyList<FluNetArchiveEntry>>(entries);
    }

    private static string TemporaryTarPath(string destination) =>
        destination + "." + Guid.NewGuid().ToString("N") + ".tmp.tar";

    private static bool PathEquals(string left, string right) => string.Equals(
        left,
        right,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static void Compress(string source, string destination)
    {
        using FileStream input = File.OpenRead(source);
        using FileStream output = File.Create(destination);
        using GZipStream gzip = new(output, CompressionLevel.Fastest);
        input.CopyTo(gzip);
    }

    private static void Decompress(string source, string destination)
    {
        using FileStream input = File.OpenRead(source);
        using GZipStream gzip = new(input, CompressionMode.Decompress);
        using FileStream output = File.Create(destination);
        gzip.CopyTo(output);
    }
}

/// <summary>Portable archive facade. ZIP remains the default; `.tar` selects TAR.</summary>
public sealed class PortableFluNetArchive(IExecutionPolicy policy) : IFluNetArchive
{
    private readonly ZipFluNetArchive zip = new(policy);
    private readonly TarFluNetArchive tar = new(policy);
    private readonly TarFluNetArchive tarGzip = new(policy, gzip: true);

    public ValueTask<string> CreateAsync(string source, string destination, CancellationToken cancellationToken = default) =>
        Select(destination).CreateAsync(source, destination, cancellationToken);

    public ValueTask<string> ExtractAsync(string source, string destination, CancellationToken cancellationToken = default) =>
        Select(source).ExtractAsync(source, destination, cancellationToken);

    public ValueTask<IReadOnlyList<FluNetArchiveEntry>> ListAsync(string source, CancellationToken cancellationToken = default) =>
        Select(source).ListAsync(source, cancellationToken);

    private IFluNetArchive Select(string path) =>
        path.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ? tarGzip :
        Path.GetExtension(path).Equals(".tar", StringComparison.OrdinalIgnoreCase) ? tar : zip;
}

public sealed class ArchiveCapabilityProvider : ICapabilityProvider
{
    public CapabilityDescriptor Descriptor { get; } = new(
        "filesystem.archive",
        "1.0",
        [FluNetPlatform.Any],
        ["filesystem.read", "filesystem.write"]);

    public bool IsAvailable => true;
}
