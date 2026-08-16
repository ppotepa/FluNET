using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using System.Text.Json;

namespace FluNET.Execution.Commands;

public sealed record SystemInfoCommand : ICommand<JsonElement>;
public sealed record SystemMetricsCommand : ICommand<JsonElement>;

public sealed class SystemInfoCommandBinder : ICommandBinder<SystemInfoCommand, JsonElement>
{
    public SystemInfoCommand? TryBind(BoundCommand command) =>
        command.Frame.Id == new FrameId("surface.system.info") ? new SystemInfoCommand() : null;
}

public sealed class SystemMetricsCommandBinder : ICommandBinder<SystemMetricsCommand, JsonElement>
{
    public SystemMetricsCommand? TryBind(BoundCommand command) =>
        command.Frame.Id == new FrameId("surface.system.metrics") ? new SystemMetricsCommand() : null;
}

public sealed class SystemInfoCommandHandler(IFluNetSystemInfoProvider provider)
    : ICommandHandler<SystemInfoCommand, JsonElement>
{
    public ValueTask<JsonElement> HandleAsync(
        SystemInfoCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(JsonSerializer.SerializeToElement(provider.Read()));
    }
}

public sealed class SystemMetricsCommandHandler : ICommandHandler<SystemMetricsCommand, JsonElement>
{
    public ValueTask<JsonElement> HandleAsync(SystemMetricsCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using System.Diagnostics.Process process = System.Diagnostics.Process.GetCurrentProcess();
        object metrics = new
        {
            processId = Environment.ProcessId,
            workingSetBytes = process.WorkingSet64,
            privateMemoryBytes = process.PrivateMemorySize64,
            managedMemoryBytes = GC.GetTotalMemory(forceFullCollection: false),
            threadCount = process.Threads.Count,
            processorTimeMilliseconds = process.TotalProcessorTime.TotalMilliseconds,
            uptimeMilliseconds = Environment.TickCount64
        };
        return ValueTask.FromResult(JsonSerializer.SerializeToElement(metrics));
    }
}
