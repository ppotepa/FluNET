using System.Diagnostics;
using System.Threading.Channels;

namespace FluNET.Capabilities;

public sealed record FluNetProcessRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    TimeSpan? Timeout = null,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string>? Environment = null);

public interface IProcessEnvironmentPolicy
{
    void EnsureVariableAccess(string name);
}

public sealed class AllowAllProcessEnvironmentPolicy : IProcessEnvironmentPolicy
{
    public void EnsureVariableAccess(string name) { }
}

public sealed record FluNetProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut);

public interface IFluNetProcessRunner
{
    ValueTask<FluNetProcessResult> RunAsync(
        FluNetProcessRequest request,
        CancellationToken cancellationToken = default);
}

public interface IFluNetProcessSession : IAsyncDisposable
{
    string Id { get; }
    bool IsRunning { get; }
    ValueTask<FluNetProcessSessionOutput> SendAsync(
        string input,
        CancellationToken cancellationToken = default);
    ValueTask<FluNetProcessResult> StopAsync(CancellationToken cancellationToken = default);
}

public sealed record FluNetProcessSessionOutput(
    string SessionId,
    string StandardOutput,
    string StandardError,
    bool IsRunning);

public interface IFluNetProcessSessionRegistry
{
    ValueTask<FluNetProcessSessionOutput> StartAsync(
        FluNetProcessRequest request,
        CancellationToken cancellationToken = default);
    ValueTask<FluNetProcessSessionOutput> SendAsync(
        string sessionId,
        string input,
        CancellationToken cancellationToken = default);
    ValueTask<FluNetProcessResult> StopAsync(
        string sessionId,
        CancellationToken cancellationToken = default);
}

public sealed class DenyFluNetProcessRunner : IFluNetProcessRunner
{
    public ValueTask<FluNetProcessResult> RunAsync(
        FluNetProcessRequest request,
        CancellationToken cancellationToken = default) =>
        throw new CapabilityDeniedException(
            "Process execution is not configured for this FluNET host.");
}

public sealed class DenyFluNetProcessSessionRegistry : IFluNetProcessSessionRegistry
{
    private static CapabilityDeniedException Denied() =>
        new("Process sessions are not configured for this FluNET host.");

    public ValueTask<FluNetProcessSessionOutput> StartAsync(
        FluNetProcessRequest request,
        CancellationToken cancellationToken = default) => throw Denied();

    public ValueTask<FluNetProcessSessionOutput> SendAsync(
        string sessionId,
        string input,
        CancellationToken cancellationToken = default) => throw Denied();

    public ValueTask<FluNetProcessResult> StopAsync(
        string sessionId,
        CancellationToken cancellationToken = default) => throw Denied();
}

/// <summary>
/// Cross-platform process runner. It starts the executable directly and never
/// invokes an operating-system shell.
/// </summary>
public sealed class PhysicalFluNetProcessRunner : IFluNetProcessRunner
{
    private readonly IExecutionPolicy policy;
    private readonly IProcessEnvironmentPolicy environmentPolicy;

    public PhysicalFluNetProcessRunner()
        : this(new AllowAllExecutionPolicy(), new AllowAllProcessEnvironmentPolicy()) { }

    public PhysicalFluNetProcessRunner(IExecutionPolicy policy)
        : this(policy, new AllowAllProcessEnvironmentPolicy()) { }

    public PhysicalFluNetProcessRunner(
        IExecutionPolicy policy,
        IProcessEnvironmentPolicy environmentPolicy)
    {
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
        this.environmentPolicy = environmentPolicy ?? throw new ArgumentNullException(nameof(environmentPolicy));
    }

    public async ValueTask<FluNetProcessResult> RunAsync(
        FluNetProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);

        string? workingDirectory = request.WorkingDirectory;
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            workingDirectory = Path.GetFullPath(workingDirectory);
            policy.EnsureFileAccess(workingDirectory);
            if (!Directory.Exists(workingDirectory))
                throw new DirectoryNotFoundException($"Working directory '{workingDirectory}' was not found.");
        }

        using Process process = new();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? string.Empty
        };
        foreach (string argument in request.Arguments)
            process.StartInfo.ArgumentList.Add(argument);
        if (request.Environment is not null)
        {
            foreach ((string name, string value) in request.Environment)
            {
                ValidateEnvironmentName(name);
                environmentPolicy.EnsureVariableAccess(name);
                process.StartInfo.Environment[name] = value;
            }
        }

        if (!process.Start())
            throw new InvalidOperationException($"Could not start process '{request.FileName}'.");

        Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);
        Task wait = process.WaitForExitAsync(cancellationToken);
        bool timedOut = false;
        if (request.Timeout is { } timeout)
        {
            Task completed = await Task.WhenAny(wait, Task.Delay(timeout, cancellationToken)).ConfigureAwait(false);
            if (completed != wait)
            {
                timedOut = true;
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
                await wait.ConfigureAwait(false);
            }
        }
        else
        {
            await wait.ConfigureAwait(false);
        }

        return new FluNetProcessResult(
            timedOut ? -1 : process.ExitCode,
            await output.ConfigureAwait(false),
            await error.ConfigureAwait(false),
            timedOut);
    }

    private static void ValidateEnvironmentName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains('=') || name.Contains('\0'))
            throw new ArgumentException($"Invalid process environment variable name '{name}'.", nameof(name));
    }
}

public sealed class PhysicalFluNetProcessSessionRegistry(
    IExecutionPolicy policy,
    IProcessEnvironmentPolicy environmentPolicy) : IFluNetProcessSessionRegistry, IAsyncDisposable
{
    private readonly object gate = new();
    private readonly Dictionary<string, PhysicalFluNetProcessSession> sessions = new(StringComparer.Ordinal);

    public async ValueTask<FluNetProcessSessionOutput> StartAsync(
        FluNetProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        PhysicalFluNetProcessSession session = await PhysicalFluNetProcessSession.StartAsync(
            request, policy, environmentPolicy, cancellationToken).ConfigureAwait(false);
        lock (gate) sessions.Add(session.Id, session);
        return await session.SnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    public ValueTask<FluNetProcessSessionOutput> SendAsync(
        string sessionId,
        string input,
        CancellationToken cancellationToken = default) =>
        Find(sessionId).SendAsync(input, cancellationToken);

    public async ValueTask<FluNetProcessResult> StopAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        PhysicalFluNetProcessSession session = Find(sessionId);
        FluNetProcessResult result = await session.StopAsync(cancellationToken).ConfigureAwait(false);
        lock (gate) sessions.Remove(sessionId);
        await session.DisposeAsync().ConfigureAwait(false);
        return result;
    }

    public async ValueTask DisposeAsync()
    {
        PhysicalFluNetProcessSession[] active;
        lock (gate)
        {
            active = sessions.Values.ToArray();
            sessions.Clear();
        }
        foreach (PhysicalFluNetProcessSession session in active)
            await session.DisposeAsync().ConfigureAwait(false);
    }

    private PhysicalFluNetProcessSession Find(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        lock (gate)
        {
            if (sessions.TryGetValue(sessionId, out PhysicalFluNetProcessSession? session)) return session;
        }
        throw new KeyNotFoundException($"Process session '{sessionId}' was not found.");
    }
}

internal sealed class PhysicalFluNetProcessSession : IFluNetProcessSession
{
    private readonly Process process;
    private readonly Channel<string> output = Channel.CreateUnbounded<string>();
    private readonly Channel<string> error = Channel.CreateUnbounded<string>();
    private readonly Task outputPump;
    private readonly Task errorPump;
    private int disposed;

    private PhysicalFluNetProcessSession(Process process)
    {
        this.process = process;
        Id = Guid.NewGuid().ToString("N");
        outputPump = PumpAsync(process.StandardOutput, output.Writer);
        errorPump = PumpAsync(process.StandardError, error.Writer);
    }

    public string Id { get; }
    public bool IsRunning => !process.HasExited;

    public static async Task<PhysicalFluNetProcessSession> StartAsync(
        FluNetProcessRequest request,
        IExecutionPolicy policy,
        IProcessEnvironmentPolicy environmentPolicy,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);
        cancellationToken.ThrowIfCancellationRequested();
        string? workingDirectory = request.WorkingDirectory;
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            workingDirectory = Path.GetFullPath(workingDirectory);
            policy.EnsureFileAccess(workingDirectory);
            if (!Directory.Exists(workingDirectory)) throw new DirectoryNotFoundException(workingDirectory);
        }

        ProcessStartInfo info = new()
        {
            FileName = request.FileName,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? string.Empty
        };
        foreach (string argument in request.Arguments) info.ArgumentList.Add(argument);
        if (request.Environment is not null)
        {
            foreach ((string name, string value) in request.Environment)
            {
                environmentPolicy.EnsureVariableAccess(name);
                info.Environment[name] = value;
            }
        }

        Process process = new() { StartInfo = info, EnableRaisingEvents = true };
        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException($"Could not start process '{request.FileName}'.");
        }
        await Task.CompletedTask.ConfigureAwait(false);
        return new PhysicalFluNetProcessSession(process);
    }

    public async ValueTask<FluNetProcessSessionOutput> SendAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed != 0, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsRunning) return await SnapshotAsync(cancellationToken).ConfigureAwait(false);
        await process.StandardInput.WriteLineAsync(input.AsMemory(), cancellationToken).ConfigureAwait(false);
        await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
        await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken).ConfigureAwait(false);
        return await SnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    public ValueTask<FluNetProcessSessionOutput> SnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new FluNetProcessSessionOutput(Id, Drain(output), Drain(error), IsRunning));
    }

    public async ValueTask<FluNetProcessResult> StopAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed != 0, this);
        if (IsRunning)
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        await Task.WhenAll(outputPump, errorPump).ConfigureAwait(false);
        return new(process.ExitCode, Drain(output), Drain(error), false);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        if (IsRunning)
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        }
        process.Dispose();
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static async Task PumpAsync(StreamReader reader, ChannelWriter<string> writer)
    {
        try
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
                await writer.WriteAsync(line).ConfigureAwait(false);
        }
        finally { writer.TryComplete(); }
    }

    private static string Drain(Channel<string> channel)
    {
        List<string> lines = [];
        while (channel.Reader.TryRead(out string? line)) lines.Add(line);
        return string.Join(Environment.NewLine, lines);
    }
}

public sealed class ProcessExecutionCapabilityProvider(
    IFluNetProcessRunner runner,
    IFluNetProcessSessionRegistry sessions)
    : ICapabilityProvider
{
    public CapabilityDescriptor Descriptor { get; } = new(
        "system.process",
        "1.0",
        [FluNetPlatform.Any],
        ["process.execute", "process.session"]);

    public bool IsAvailable =>
        runner is not DenyFluNetProcessRunner ||
        sessions is not DenyFluNetProcessSessionRegistry;
}
