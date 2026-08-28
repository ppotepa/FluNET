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
