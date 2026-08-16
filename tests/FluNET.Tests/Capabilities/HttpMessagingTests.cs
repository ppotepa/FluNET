using FluNET.Capabilities;
using System.Text;
using System.Text.Json;

namespace FluNET.Tests.Capabilities;

[TestFixture]
public sealed class HttpMessagingTests
{
    [Test]
    public async Task HttpBusPublishesAndReceivesProviderNeutralMessages()
    {
        FakeHttpTransport transport = new();
        HttpFluNetMessageBus bus = new(
            new Uri("https://queue.example.test/events"),
            transport,
            new DenyAuthenticatedTransport());

        await bus.PublishAsync("orders/created", "{\"id\":1}");
        FluNetMessage message = await bus.ReceiveAsync("orders/created");

        Assert.Multiple(() =>
        {
            Assert.That(transport.LastPostUri, Is.EqualTo(new Uri("https://queue.example.test/events/orders%2Fcreated")));
            Assert.That(message.Topic, Is.EqualTo("orders/created"));
            Assert.That(message.Payload, Is.EqualTo("{\"id\":1}"));
        });
    }

    private sealed class FakeHttpTransport : IHttpTransport
    {
        private readonly Queue<FluNetMessage> messages = new();
        public Uri? LastPostUri { get; private set; }

        public Task<HttpResourceResponse> GetAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            FluNetMessage message = messages.Dequeue();
            return Task.FromResult(Response(message));
        }

        public Task<byte[]> GetBytesAsync(Uri uri, CancellationToken cancellationToken = default) =>
            GetAsync(uri, cancellationToken).ContinueWith(task => task.Result.Content, cancellationToken);

        public Task<string> PostJsonAsync(Uri uri, string json, CancellationToken cancellationToken = default)
        {
            LastPostUri = uri;
            using JsonDocument document = JsonDocument.Parse(json);
            messages.Enqueue(new(
                document.RootElement.GetProperty("topic").GetString()!,
                document.RootElement.GetProperty("payload").GetString()!,
                "message-1",
                DateTimeOffset.UtcNow));
            return Task.FromResult("accepted");
        }

        private static HttpResourceResponse Response(FluNetMessage message) => new(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message)),
            200,
            "application/json",
            "utf-8",
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase));
    }

    private sealed class DenyAuthenticatedTransport : IAuthenticatedHttpTransport
    {
        public Task<HttpResourceResponse> GetAsync(Uri uri, SecretValue credential, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
