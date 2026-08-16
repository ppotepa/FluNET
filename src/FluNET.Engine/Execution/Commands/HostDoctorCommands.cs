using System.Reflection;
using System.Text.Json;
using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;

namespace FluNET.Execution.Commands;

public sealed record HostDoctorCommand : ICommand<JsonElement>;

public sealed class HostDoctorCommandBinder : ICommandBinder<HostDoctorCommand, JsonElement>
{
    public HostDoctorCommand? TryBind(BoundCommand command) =>
        command.Frame.Id == new FrameId("surface.system.doctor") ? new HostDoctorCommand() : null;
}

public sealed class HostDoctorCommandHandler(
    CapabilityRegistry capabilities,
    IFluNetProviderPackageCatalog packages,
    IFluNetSystemInfoProvider system,
    IFluNetBlobStore blobs,
    IFluNetFileMetadataIndex index,
    IFluNetMessageBus messages) : ICommandHandler<HostDoctorCommand, JsonElement>
{
    public ValueTask<JsonElement> HandleAsync(HostDoctorCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<CapabilityDescriptor> descriptors = capabilities.Describe();
        int available = descriptors.Count(descriptor => capabilities.TryResolve(descriptor.Id, out _));
        object report = new
        {
            status = "ok",
            runtime = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown",
            framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            platform = FluNetPlatformInfo.Current.ToString(),
            system = system.Read(),
            capabilities = new { total = descriptors.Count, available, denied = descriptors.Count - available },
            packages = packages.Discover().Count,
            providers = new { blob = blobs.GetType().FullName, index = index.GetType().FullName, messageBus = messages.GetType().FullName }
        };
        return ValueTask.FromResult(JsonSerializer.SerializeToElement(report));
    }
}
