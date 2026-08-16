using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;

namespace FluNET.Capabilities;

public sealed record FluNetMessage(
    string Topic,
    string Payload,
    string MessageId,
    DateTimeOffset PublishedUtc);

public interface IFluNetMessageBus
{
    ValueTask PublishAsync(
        string topic,
        string payload,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<FluNetMessage> ReadAsync(
        string topic,
        CancellationToken cancellationToken = default);

    async ValueTask<FluNetMessage> ReceiveAsync(
        string topic,
        CancellationToken cancellationToken = default)
    {
        await using IAsyncEnumerator<FluNetMessage> messages =
            ReadAsync(topic, cancellationToken).GetAsyncEnumerator(cancellationToken);
        if (await messages.MoveNextAsync().ConfigureAwait(false)) return messages.Current;
        throw new InvalidOperationException($"Message stream for topic '{topic}' ended.");
    }
}

/// <summary>
/// Portable host-local message bus. Hosts can replace this contract with a
/// durable or remote transport while preserving the PUBLISH surface command.
/// </summary>
public sealed class InMemoryFluNetMessageBus : IFluNetMessageBus
{
    private readonly ConcurrentDictionary<string, Channel<FluNetMessage>> channels =
        new(StringComparer.OrdinalIgnoreCase);

    public async ValueTask PublishAsync(
        string topic,
        string payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(payload);
        Channel<FluNetMessage> channel = channels.GetOrAdd(topic, _ => Channel.CreateUnbounded<FluNetMessage>());
        await channel.Writer.WriteAsync(
            new(topic, payload, Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow),
            cancellationToken).ConfigureAwait(false);
    }

    public IAsyncEnumerable<FluNetMessage> ReadAsync(
        string topic,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        Channel<FluNetMessage> channel = channels.GetOrAdd(topic, _ => Channel.CreateUnbounded<FluNetMessage>());
        return channel.Reader.ReadAllAsync(cancellationToken);
    }
}

/// <summary>
/// Single-host durable queue backed by one JSONL file. It is intentionally
/// provider-neutral and policy-checked; distributed brokers belong in host
/// integrations implementing the same interface.
/// </summary>
public sealed class JsonFileFluNetMessageBus : IFluNetMessageBus
{
    private readonly string path;
    private readonly IExecutionPolicy policy;
    private readonly SemaphoreSlim gate = new(1, 1);

    public JsonFileFluNetMessageBus(string path, IExecutionPolicy policy)
    {
        this.path = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public async ValueTask PublishAsync(
        string topic,
        string payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(payload);
        cancellationToken.ThrowIfCancellationRequested();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            policy.EnsureFileAccess(path);
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            FluNetMessage message = new(topic, payload, Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow);
            await File.AppendAllTextAsync(path, JsonSerializer.Serialize(message) + Environment.NewLine, cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public async ValueTask<FluNetMessage> ReceiveAsync(
        string topic,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        while (true)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                policy.EnsureFileAccess(path);
                List<FluNetMessage> messages = await ReadMessagesAsync(cancellationToken).ConfigureAwait(false);
                int index = messages.FindIndex(message => message.Topic.Equals(topic, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    FluNetMessage message = messages[index];
                    messages.RemoveAt(index);
                    await WriteMessagesAsync(messages, cancellationToken).ConfigureAwait(false);
                    return message;
                }
            }
            finally { gate.Release(); }
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
        }
    }

    public async IAsyncEnumerable<FluNetMessage> ReadAsync(
        string topic,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (true)
            yield return await ReceiveAsync(topic, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<List<FluNetMessage>> ReadMessagesAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return [];
        List<FluNetMessage> messages = [];
        foreach (string line in await File.ReadAllLinesAsync(path, cancellationToken).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            FluNetMessage? message = JsonSerializer.Deserialize<FluNetMessage>(line);
            if (message is not null) messages.Add(message);
        }
        return messages;
    }

    private async ValueTask WriteMessagesAsync(List<FluNetMessage> messages, CancellationToken cancellationToken)
    {
        string directory = Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllLinesAsync(temporary, messages.Select(message => JsonSerializer.Serialize(message)), cancellationToken).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}

public sealed class MessageBusCapabilityProvider(IFluNetMessageBus bus) : ICapabilityProvider
{
    public CapabilityDescriptor Descriptor { get; } = new(
        "messaging.queue",
        "1.0",
        [FluNetPlatform.Any],
        ["messaging.publish", "messaging.consume"]);

    public bool IsAvailable => bus is not null;
}
