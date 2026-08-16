using System.Collections.Concurrent;

namespace FluNET.Capabilities;

public sealed record FluNetTempArtifact(string Path, bool IsDirectory);

public interface IFluNetTemporaryArtifacts
{
    ValueTask<FluNetTempArtifact> CreateFileAsync(
        string? suffix = null,
        CancellationToken cancellationToken = default);
    ValueTask<FluNetTempArtifact> CreateDirectoryAsync(
        CancellationToken cancellationToken = default);
    ValueTask CleanupAsync(string path, CancellationToken cancellationToken = default);
}

public sealed class PhysicalFluNetTemporaryArtifacts(IExecutionPolicy policy) : IFluNetTemporaryArtifacts, IDisposable, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, bool> owned = new(StringComparer.OrdinalIgnoreCase);

    public ValueTask<FluNetTempArtifact> CreateFileAsync(
        string? suffix = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string root = Path.GetFullPath(Path.GetTempPath());
        policy.EnsureFileAccess(root);
        string normalizedSuffix = NormalizeSuffix(suffix);
        string path;
        do
        {
            path = Path.Combine(root, $"flunet-{Guid.NewGuid():N}{normalizedSuffix}");
        }
        while (File.Exists(path) || Directory.Exists(path));
        policy.EnsureFileAccess(path);
        using (File.Create(path)) { }
        owned[path] = false;
        return ValueTask.FromResult(new FluNetTempArtifact(path, false));
    }

    public ValueTask<FluNetTempArtifact> CreateDirectoryAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string root = Path.GetFullPath(Path.GetTempPath());
        policy.EnsureFileAccess(root);
        string path;
        do
        {
            path = Path.Combine(root, $"flunet-{Guid.NewGuid():N}");
        }
        while (Directory.Exists(path) || File.Exists(path));
        policy.EnsureFileAccess(path);
        Directory.CreateDirectory(path);
        owned[path] = true;
        return ValueTask.FromResult(new FluNetTempArtifact(path, true));
    }

    public ValueTask CleanupAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fullPath = Path.GetFullPath(path);
        if (!owned.TryRemove(fullPath, out bool isDirectory))
            throw new InvalidOperationException($"Path '{fullPath}' is not an artifact owned by system.temp.");

        DeleteOwnedPath(fullPath, isDirectory);
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        foreach (KeyValuePair<string, bool> artifact in owned.ToArray())
        {
            if (!owned.TryRemove(artifact.Key, out _)) continue;
            try { DeleteOwnedPath(artifact.Key, artifact.Value); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private void DeleteOwnedPath(string path, bool isDirectory)
    {
        policy.EnsureFileAccess(path);
        if (isDirectory)
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string NormalizeSuffix(string? suffix)
    {
        if (string.IsNullOrWhiteSpace(suffix)) return string.Empty;
        string value = suffix.Trim();
        if (!value.StartsWith(".", StringComparison.Ordinal) ||
            value.Length > 32 ||
            value.Any(character => character is '/' or '\\' or ':' or '\0'))
            throw new ArgumentException("Temporary file suffix must be a short extension such as '.json'.", nameof(suffix));
        return value;
    }
}

public sealed class TemporaryArtifactsCapabilityProvider : ICapabilityProvider
{
    public CapabilityDescriptor Descriptor { get; } = new(
        "system.temp",
        "1.0",
        [FluNetPlatform.Any],
        ["filesystem.write"]);

    public bool IsAvailable => true;
}
