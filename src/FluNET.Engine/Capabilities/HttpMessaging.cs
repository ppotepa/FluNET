using System.Text;
using System.Text.Json;

namespace FluNET.Capabilities;

/// <summary>
/// REST queue adapter with a deliberately small provider-neutral convention:
/// POST /{topic} accepts {topic,payload}; GET /{topic} returns one FluNetMessage
/// or an empty response when the queue is empty.
/// </summary>
public sealed class HttpFluNetMessageBus : IFluNetMessageBus
{
    private readonly Uri endpoint;
    private readonly IHttpTransport transport;
    private readonly IAuthenticatedHttpTransport authenticated;
    private readonly SecretValue? credential;

    public HttpFluNetMessageBus(
        Uri endpoint,
        IHttpTransport transport,
        IAuthenticatedHttpTransport authenticated,
        SecretValue? credential = null)
    {
        if (endpoint is null || !endpoint.IsAbsoluteUri || endpoint.Scheme is not ("http" or "https"))
            throw new ArgumentException("Queue endpoint must be an absolute HTTP(S) URI.", nameof(endpoint));
        this.endpoint = endpoint.AbsoluteUri.EndsWith('/') ? endpoint : new Uri(endpoint.AbsoluteUri + "/");
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.authenticated = authenticated ?? throw new ArgumentNullException(nameof(authenticated));
        this.credential = credential;
    }

    public async ValueTask PublishAsync(string topic, string payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(payload);
        string body = JsonSerializer.Serialize(new { topic, payload });
        await SendPostAsync(BuildTopicUri(topic), body, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<FluNetMessage> ReceiveAsync(string topic, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        while (true)
        {
            HttpResourceResponse response = credential is null
                ? await transport.GetAsync(BuildTopicUri(topic), cancellationToken).ConfigureAwait(false)
                : await authenticated.GetAsync(BuildTopicUri(topic), credential, cancellationToken).ConfigureAwait(false);
            if (response.Content.Length > 0)
            {
                FluNetMessage? message = JsonSerializer.Deserialize<FluNetMessage>(response.Content);
                if (message is not null) return message;
                throw new InvalidDataException("HTTP queue returned an invalid FluNET message.");
            }
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
        }
    }

    public async IAsyncEnumerable<FluNetMessage> ReadAsync(
        string topic,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (true) yield return await ReceiveAsync(topic, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask SendPostAsync(Uri uri, string body, CancellationToken cancellationToken)
    {
        if (credential is null)
            await transport.PostJsonAsync(uri, body, cancellationToken).ConfigureAwait(false);
        else
            await authenticated.PostJsonAsync(uri, body, credential, cancellationToken).ConfigureAwait(false);
    }

    private Uri BuildTopicUri(string topic) => new(endpoint, Uri.EscapeDataString(topic));
}
