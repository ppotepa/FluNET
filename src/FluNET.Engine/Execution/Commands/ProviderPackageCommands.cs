using System.Text.Json;
using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;

namespace FluNET.Execution.Commands;

public sealed record ProviderPackageSnapshotCommand : ICommand<JsonElement[]>;

public sealed class ProviderPackageSnapshotCommandBinder : ICommandBinder<ProviderPackageSnapshotCommand, JsonElement[]>
{
    public ProviderPackageSnapshotCommand? TryBind(BoundCommand command) =>
        command.Frame.Id == new FrameId("surface.system.packages") ? new ProviderPackageSnapshotCommand() : null;
}

public sealed class ProviderPackageSnapshotCommandHandler(IFluNetProviderPackageCatalog catalog)
    : ICommandHandler<ProviderPackageSnapshotCommand, JsonElement[]>
{
    public ValueTask<JsonElement[]> HandleAsync(ProviderPackageSnapshotCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(catalog.Discover().Select(package => JsonSerializer.SerializeToElement(new
        {
            id = package.Id,
            version = package.Version,
            entryPoint = package.EntryPoint,
            available = package.SupportsCurrentPlatform,
            platforms = package.Platforms,
            capabilities = package.Capabilities,
            permissions = package.Permissions
        })).ToArray());
    }
}
