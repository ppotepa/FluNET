using FluNET.Capabilities;
using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Language.Values;
using FluNET.Variables;

namespace FluNET.Execution.Commands;

public sealed record ReadClipboardCommand : ICommand<string>;

public sealed class ReadClipboardCommandBinder : ICommandBinder<ReadClipboardCommand, string>
{
    public ReadClipboardCommand? TryBind(BoundCommand command) =>
        command.Frame.Id == new FrameId("surface.system.clipboard.read")
            ? new ReadClipboardCommand()
            : null;
}

public sealed class ReadClipboardCommandHandler(IFluNetClipboard clipboard)
    : ICommandHandler<ReadClipboardCommand, string>
{
    public async ValueTask<string> HandleAsync(
        ReadClipboardCommand command,
        CancellationToken cancellationToken = default)
    {
        string? value = await clipboard.ReadTextAsync(cancellationToken).ConfigureAwait(false);
        return value ?? throw new CapabilityUnavailableException(
            "The host does not expose a readable text clipboard.");
    }
}
