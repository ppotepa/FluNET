using FluNET.Capabilities;
using FluNET.Execution.Planning;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace FluNET.Declarative;

public sealed record EnsureVersion(
    string Target,
    DateTimeOffset CapturedAt,
    string Content);

public interface IEnsureVersionStore
{
    ValueTask CaptureAsync(
        EnsureVersion version,
        int keepVersions,
        CancellationToken cancellationToken = default);
}

public sealed class InMemoryEnsureVersionStore : IEnsureVersionStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, List<EnsureVersion>> _versions =
        new(StringComparer.OrdinalIgnoreCase);

    public ValueTask CaptureAsync(
        EnsureVersion version,
        int keepVersions,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (keepVersions <= 0) throw new ArgumentOutOfRangeException(nameof(keepVersions));
        lock (_gate)
        {
            if (!_versions.TryGetValue(version.Target, out List<EnsureVersion>? versions))
            {
                versions = [];
                _versions[version.Target] = versions;
            }
            versions.Add(version);
            versions.Sort((left, right) => right.CapturedAt.CompareTo(left.CapturedAt));
            if (versions.Count > keepVersions)
                versions.RemoveRange(keepVersions, versions.Count - keepVersions);
        }
        return ValueTask.CompletedTask;
    }

    public IReadOnlyList<EnsureVersion> Snapshot(string target)
    {
        lock (_gate)
        {
            return _versions.TryGetValue(target, out List<EnsureVersion>? versions)
                ? versions.ToArray()
                : Array.Empty<EnsureVersion>();
        }
    }
}

/// <summary>Optional durable version store for ENSURE file targets.</summary>
public sealed class DirectoryEnsureVersionStore(
    string directory,
    IExecutionPolicy policy) : IEnsureVersionStore
{
    private readonly string _directory = Path.GetFullPath(
        string.IsNullOrWhiteSpace(directory)
            ? throw new ArgumentException("A version-store directory is required.", nameof(directory))
            : directory);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks =
        new(StringComparer.OrdinalIgnoreCase);

    public async ValueTask CaptureAsync(
        EnsureVersion version,
        int keepVersions,
        CancellationToken cancellationToken = default)
    {
        if (keepVersions <= 0) throw new ArgumentOutOfRangeException(nameof(keepVersions));
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            Path.GetFullPath(version.Target)))).ToLowerInvariant();
        string folder = Path.Combine(_directory, key);
        string probe = Path.Combine(folder, ".access");
        policy.EnsureFileAccess(probe);
        SemaphoreSlim gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(folder);
            string file = Path.Combine(
                folder,
                $"{version.CapturedAt.UtcTicks:D20}-{Guid.NewGuid():N}.bak");
            policy.EnsureFileAccess(file);
            await File.WriteAllTextAsync(file, version.Content, cancellationToken)
                .ConfigureAwait(false);

            string[] existing = Directory.EnumerateFiles(folder, "*.bak")
                .OrderByDescending(path => Path.GetFileName(path), StringComparer.Ordinal)
                .ToArray();
            foreach (string obsolete in existing.Skip(keepVersions))
            {
                policy.EnsureFileAccess(obsolete);
                File.Delete(obsolete);
            }
        }
        finally { gate.Release(); }
    }
}

public interface IDesiredStateNotifier
{
    ValueTask NotifyFailureAsync(
        EnsureGoal goal,
        Exception error,
        CancellationToken cancellationToken = default);
}

public sealed class TextOutputDesiredStateNotifier(ITextOutput output)
    : IDesiredStateNotifier
{
    public ValueTask NotifyFailureAsync(
        EnsureGoal goal,
        Exception error,
        CancellationToken cancellationToken = default) =>
        output.WriteLineAsync(
            $"ENSURE failed for '{goal.Target}': {error.Message}",
            cancellationToken);
}

public sealed record EnsureRunResult(
    DesiredStatePlan Plan,
    IReadOnlyList<ExecutionStepResult> Steps,
    object? Result,
    Exception? Error)
{
    public bool IsSuccess => Plan.IsValid && Error is null;
}

public sealed class EnsureRunner(
    SentenceExecutor executor,
    IFluNetFileSystem files,
    IEnsureVersionStore versions,
    IDesiredStateNotifier notifier)
{
    public async ValueTask<EnsureRunResult> RunAsync(
        DesiredStatePlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        List<ExecutionStepResult> steps = [];
        if (!plan.IsValid || plan.Compilation.Plan is null)
            return new(plan, steps, null,
                new InvalidOperationException("ENSURE plan is not valid."));

        bool localFile = !Uri.TryCreate(plan.Goal.Target, UriKind.Absolute, out _);
        bool existed = false;
        string? previous = null;
        if (localFile && plan.Goal.KeepVersions is > 0)
        {
            existed = await files.FileExistsAsync(plan.Goal.Target, cancellationToken)
                .ConfigureAwait(false);
            if (existed)
                previous = await files.ReadAllTextAsync(plan.Goal.Target, cancellationToken)
                    .ConfigureAwait(false);
        }

        try
        {
            object? result = await executor.ExecuteAsync(
                plan.Compilation.Plan,
                steps,
                cancellationToken).ConfigureAwait(false);

            if (existed && previous is not null && plan.Goal.KeepVersions is int keep)
            {
                string? current = result as string;
                if (!string.Equals(previous, current, StringComparison.Ordinal))
                {
                    await versions.CaptureAsync(
                        new EnsureVersion(
                            plan.Goal.Target,
                            DateTimeOffset.UtcNow,
                            previous),
                        keep,
                        cancellationToken).ConfigureAwait(false);
                }
            }
            return new(plan, steps, result, null);
        }
        catch (Exception exception)
        {
            if (plan.Goal.NotifyOnFailure)
            {
                try
                {
                    await notifier.NotifyFailureAsync(
                        plan.Goal,
                        exception,
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception notificationFailure)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"FluNET desired-state notification failed: {notificationFailure}");
                }
            }
            return new(plan, steps, null, exception);
        }
    }
}

public static class EnsureRuntimeExtensions
{
    public static IServiceCollection AddDirectoryEnsureVersions(
        this IServiceCollection services,
        string directory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        services.AddSingleton<IEnsureVersionStore>(provider =>
            new DirectoryEnsureVersionStore(
                directory,
                provider.GetRequiredService<IExecutionPolicy>()));
        return services;
    }
}
