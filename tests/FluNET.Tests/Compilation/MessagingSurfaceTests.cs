using FluNET.Capabilities;
using FluNET.Context;
using System.Runtime.CompilerServices;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class MessagingSurfaceTests
{
    [Test]
    public async Task PublishUsesThePortableMessageBusCapability()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(
            "PUBLISH \"backup completed\" TO \"events\"");

        Assert.That(execution.IsSuccess, Is.True,
            execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Diagnostics.Select(item => item.Message)));
        Assert.That(context.GetService<CapabilityRegistry>().TryResolve("messaging.queue", out _), Is.True);

        IFluNetMessageBus bus = context.GetService<IFluNetMessageBus>();
        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(2));
        await using IAsyncEnumerator<FluNetMessage> messages = bus.ReadAsync("events", cancellation.Token).GetAsyncEnumerator(cancellation.Token);

        Assert.That(await messages.MoveNextAsync().AsTask(), Is.True);
        Assert.That(messages.Current.Payload, Is.EqualTo("backup completed"));
        Assert.That(messages.Current.Topic, Is.EqualTo("events"));
    }

    [Test]
    public async Task ReceiveCanConsumeAPreviouslyPublishedSurfaceMessage()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();

        SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync("""
PUBLISH "ready" TO "events"
THEN
RECEIVE "events" AS message
""");

        Assert.That(execution.IsSuccess, Is.True,
            execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Diagnostics.Select(item => item.Message)));
        System.Text.Json.JsonElement message = (System.Text.Json.JsonElement)execution.Result!;
        Assert.That(message.GetProperty("Payload").GetString(), Is.EqualTo("ready"));
        Assert.That(message.GetProperty("Topic").GetString(), Is.EqualTo("events"));
    }
}
