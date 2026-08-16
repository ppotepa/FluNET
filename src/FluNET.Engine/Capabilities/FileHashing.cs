using System.Security.Cryptography;

namespace FluNET.Capabilities;

public interface IFluNetFileHasher
{
    ValueTask<string> ComputeSha256Async(string path, CancellationToken cancellationToken = default);
}

public sealed class PhysicalFluNetFileHasher(IExecutionPolicy policy) : IFluNetFileHasher
{
    public async ValueTask<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fullPath = Path.GetFullPath(path);
        policy.EnsureFileAccess(fullPath);
        await using FileStream stream = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 64 * 1024, options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public sealed class FileHashCapabilityProvider : ICapabilityProvider
{
    public CapabilityDescriptor Descriptor { get; } = new(
        "filesystem.hash",
        "1.0",
        [FluNetPlatform.Any],
        ["filesystem.read"]);

    public bool IsAvailable => true;
}
