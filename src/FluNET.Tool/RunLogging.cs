using FluNET.Telemetry;

namespace FluNET.Tool;

/// <summary>Human-readable stderr sink for interactive CLI execution tracing.</summary>
public sealed class ConsoleFluNetTelemetrySink(int verbosity) : IFluNetTelemetrySink
{
    public ValueTask EmitAsync(FluNetTelemetryEvent item, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (item.Category == "execution" && item.Name == "step" && verbosity >= 1)
        {
            Console.Error.WriteLine(
                $"[trace] step #{item.Attributes.GetValueOrDefault("step.index", "?")} " +
                $"{item.Outcome} {item.Attributes.GetValueOrDefault("frame.id", "?")} " +
                $"attempt={item.Attributes.GetValueOrDefault("attempt", "?")} " +
                $"duration={item.Duration.TotalMilliseconds:0.##}ms");
        }
        else if (item.Category == "command" && item.Name == "dispatch" && verbosity >= 3)
        {
            Console.Error.WriteLine(
                $"[trace] dispatch {item.Attributes.GetValueOrDefault("frame.id", "?")} " +
                $"{item.Outcome} duration={item.Duration.TotalMilliseconds:0.##}ms");
        }
        return ValueTask.CompletedTask;
    }
}
