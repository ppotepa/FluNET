namespace FluNET.Capabilities;

public interface IFluNetClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IFluNetDelay
{
    ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken = default);
}

public sealed class SystemFluNetClock : IFluNetClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class SystemFluNetDelay : IFluNetDelay
{
    public async ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        await Task.Delay(duration, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class TimeCapabilityProvider : ICapabilityProvider
{
    public CapabilityDescriptor Descriptor { get; } = new(
        "system.time",
        "1.0",
        [FluNetPlatform.Any],
        ["system.time.read", "system.time.wait"]);

    public bool IsAvailable => true;
}
