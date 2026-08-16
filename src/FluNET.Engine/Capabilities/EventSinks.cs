namespace FluNET.Capabilities;

public interface IFluNetEventSink
{
    ValueTask<string> EmitAsync(Uri endpoint, string payload, SecretValue? credential = null, CancellationToken cancellationToken = default);
}

/// <summary>Default portable sink. Hosts can replace it with a broker or native integration.</summary>
public sealed class HttpWebhookEventSink(IHttpTransport transport, IAuthenticatedHttpTransport authenticated) : IFluNetEventSink
{
    public async ValueTask<string> EmitAsync(Uri endpoint, string payload, SecretValue? credential = null, CancellationToken cancellationToken = default) =>
        credential is null
            ? await transport.PostJsonAsync(endpoint, payload, cancellationToken).ConfigureAwait(false)
            : await authenticated.PostJsonAsync(endpoint, payload, credential, cancellationToken).ConfigureAwait(false);
}

public sealed class EventSinkCapabilityProvider(IFluNetEventSink sink) : ICapabilityProvider
{
    public CapabilityDescriptor Descriptor { get; } = new(
        "events.sink", "1.0", [FluNetPlatform.Any], ["network.connect", "events.emit"]);

    public bool IsAvailable => sink is not null;
}
