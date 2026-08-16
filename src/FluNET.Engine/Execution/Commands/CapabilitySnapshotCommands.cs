using System.Text.Json;
using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;

namespace FluNET.Execution.Commands;

public sealed record CapabilitySnapshotCommand : ICommand<JsonElement[]>;

public sealed class CapabilitySnapshotCommandBinder : ICommandBinder<CapabilitySnapshotCommand, JsonElement[]>
{
    public CapabilitySnapshotCommand? TryBind(BoundCommand command) =>
        command.Frame.Id == new FrameId("surface.system.capabilities") ? new CapabilitySnapshotCommand() : null;
}

public sealed class CapabilitySnapshotCommandHandler(CapabilityRegistry registry)
    : ICommandHandler<CapabilitySnapshotCommand, JsonElement[]>
{
    public ValueTask<JsonElement[]> HandleAsync(CapabilitySnapshotCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FluNetPlatform current = FluNetPlatformInfo.Current;
        return ValueTask.FromResult(registry.Describe().Select(descriptor => JsonSerializer.SerializeToElement(new
        {
            id = descriptor.Id,
            version = descriptor.Version,
            available = registry.TryResolve(descriptor.Id, out _, current),
            platform = current.ToString(),
            platforms = descriptor.Platforms,
            permissions = descriptor.Permissions
        })).ToArray());
    }
}
