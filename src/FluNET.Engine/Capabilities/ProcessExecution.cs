using System.Diagnostics;
using System.Threading.Channels;

namespace FluNET.Capabilities;

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
        ValidateTimeout(request.Timeout);

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
        ApplyEnvironment(process.StartInfo, request.Environment, environmentPolicy);

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
                TryKill(process);
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

    internal static void ApplyEnvironment(
        ProcessStartInfo startInfo,
        IReadOnlyDictionary<string, string>? environment,
        IProcessEnvironmentPolicy environmentPolicy)
    {
        if (environment is null)
            return;

        foreach ((string name, string value) in environment)
        {
            ValidateEnvironmentName(name);
            environmentPolicy.EnsureVariableAccess(name);
            startInfo.Environment[name] = value;
        }
    }

    internal static void ValidateEnvironmentName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains('=') || name.Contains('\0'))
            throw new ArgumentException($"Invalid process environment variable name '{name}'.", nameof(name));
    }

    internal static void ValidateTimeout(TimeSpan? timeout)
    {
        if (timeout is { } value && value < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Process timeout cannot be negative.");
    }

    internal static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
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
        lock (gate)
            sessions.Add(session.Id, session);
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
        lock (gate)
            sessions.Remove(sessionId);
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
            if (sessions.TryGetValue(sessionId, out PhysicalFluNetProcessSession? session))
                return session;
        }
        throw new KeyNotFoundException($"Process session '{sessionId}' was not found.");
    }
}

internal sealed class PhysicalFluNetProcessSession : IFluNetProcessSession
{
    private const int BufferedLineCapacity = 1024;

    private readonly Process process;
    private readonly Channel<string> output = CreateOutputChannel();
    private readonly Channel<string> error = CreateOutputChannel();
    private readonly CancellationTokenSource lifetime = new();
    private readonly Task outputPump;
    private readonly Task errorPump;
    private readonly Task timeoutMonitor;
    private int disposed;
    private int timedOut;

    private PhysicalFluNetProcessSession(Process process, TimeSpan? timeout)
    {
        this.process = process;
        Id = Guid.NewGuid().ToString("N");
        outputPump = PumpAsync(process.StandardOutput, output.Writer);
        errorPump = PumpAsync(process.StandardError, error.Writer);
        timeoutMonitor = timeout is { } value
            ? MonitorTimeoutAsync(value)
            : Task.CompletedTask;
    }

    public string Id { get; }
    public bool IsRunning => !process.HasExited;

    public static Task<PhysicalFluNetProcessSession> StartAsync(
        FluNetProcessRequest request,
        IExecutionPolicy policy,
        IProcessEnvironmentPolicy environmentPolicy,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);
        cancellationToken.ThrowIfCancellationRequested();
        PhysicalFluNetProcessRunner.ValidateTimeout(request.Timeout);

        string? workingDirectory = request.WorkingDirectory;
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            workingDirectory = Path.GetFullPath(workingDirectory);
            policy.EnsureFileAccess(workingDirectory);
            if (!Directory.Exists(workingDirectory))
                throw new DirectoryNotFoundException(workingDirectory);
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
        foreach (string argument in request.Arguments)
            info.ArgumentList.Add(argument);
        PhysicalFluNetProcessRunner.ApplyEnvironment(info, request.Environment, environmentPolicy);

        Process process = new() { StartInfo = info, EnableRaisingEvents = true };
        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException($"Could not start process '{request.FileName}'.");
        }

        return Task.FromResult(new PhysicalFluNetProcessSession(process, request.Timeout));
    }

    public async ValueTask<FluNetProcessSessionOutput> SendAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed != 0, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsRunning)
            return await SnapshotAsync(cancellationToken).ConfigureAwait(false);

        await process.StandardInput.WriteLineAsync(input.AsMemory(), cancellationToken).ConfigureAwait(false);
        await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
        await Task.Yield();
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
            PhysicalFluNetProcessRunner.TryKill(process);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }

        lifetime.Cancel();
        await ObserveMonitorAsync().ConfigureAwait(false);
        await Task.WhenAll(outputPump, errorPump).ConfigureAwait(false);
        return new(process.ExitCode, Drain(output), Drain(error), Volatile.Read(ref timedOut) != 0);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;

        try
        {
            if (IsRunning)
            {
                PhysicalFluNetProcessRunner.TryKill(process);
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            lifetime.Cancel();
            await ObserveMonitorAsync().ConfigureAwait(false);
            await Task.WhenAll(outputPump, errorPump).ConfigureAwait(false);
            process.Dispose();
            lifetime.Dispose();
        }
    }

    private async Task MonitorTimeoutAsync(TimeSpan timeout)
    {
        try
        {
            await Task.Delay(timeout, lifetime.Token).ConfigureAwait(false);
            if (lifetime.IsCancellationRequested || process.HasExited)
                return;

            Interlocked.Exchange(ref timedOut, 1);
            PhysicalFluNetProcessRunner.TryKill(process);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private async Task ObserveMonitorAsync()
    {
        try
        {
            await timeoutMonitor.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
    }

    private static Channel<string> CreateOutputChannel() =>
        Channel.CreateBounded<string>(new BoundedChannelOptions(BufferedLineCapacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });

    private static async Task PumpAsync(StreamReader reader, ChannelWriter<string> writer)
    {
        try
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
                await writer.WriteAsync(line).ConfigureAwait(false);
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private static string Drain(Channel<string> channel)
    {
        List<string> lines = [];
        while (channel.Reader.TryRead(out string? line))
            lines.Add(line);
        return string.Join(Environment.NewLine, lines);
    }
}
