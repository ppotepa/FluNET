namespace FluNET.Capabilities;

public interface IFluNetNotifier
{
    ValueTask NotifyAsync(
        string message,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Portable fallback. Hosts can replace this contract with a native desktop,
/// service, or remote notification provider without changing the language.
/// </summary>
public sealed class ConsoleFluNetNotifier(ITextOutput output) : IFluNetNotifier
{
    public ValueTask NotifyAsync(
        string message,
        CancellationToken cancellationToken = default) =>
        output.WriteLineAsync($"[NOTIFY] {message}", cancellationToken);
}

public sealed class NotificationCapabilityProvider(IFluNetNotifier notifier) : ICapabilityProvider
{
    public CapabilityDescriptor Descriptor { get; } = new(
        "system.notify",
        "1.0",
        [FluNetPlatform.Any],
        ["system.notify"]);

    public bool IsAvailable => notifier is not null;
}
