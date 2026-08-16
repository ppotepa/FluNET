namespace FluNET.Capabilities;

public sealed class NetworkCapabilityProvider(IHttpTransport transport) : ICapabilityProvider
{
    public CapabilityDescriptor Descriptor { get; } = new(
        "network.http",
        "1.0",
        [FluNetPlatform.Any],
        ["network.connect"]);

    public bool IsAvailable => transport is not null;
}
