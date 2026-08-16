using FluNET.Capabilities;
using System.Net;
using System.Net.Http;

namespace FluNET.Tests.Capabilities;

[TestFixture]
public sealed class BlobStorageTests
{
    [Test]
    public async Task InMemoryBlobStoreSupportsPutGetAndDelete()
    {
        IFluNetBlobStore store = new InMemoryFluNetBlobStore();

        await store.PutAsync("reports/latest", "hello");

        Assert.That(await store.GetAsync("reports/latest"), Is.EqualTo("hello"));
        Assert.That(await store.DeleteAsync("reports/latest"), Is.True);
        Assert.That(await store.GetAsync("reports/latest"), Is.Null);
    }

    [Test]
    public async Task InMemoryBlobStoreListsKeysByPortablePrefix()
    {
        IFluNetBlobStore store = new InMemoryFluNetBlobStore();
        await store.PutAsync("reports/old", "1");
        await store.PutAsync("reports/latest", "2");
        await store.PutAsync("other/item", "3");

        Assert.That(await store.ListAsync("reports/"), Is.EqualTo(new[] { "reports/latest", "reports/old" }));
    }

    [TestCase("../escape")]
    [TestCase("/absolute")]
    [TestCase("..\\escape")]
    public void InMemoryBlobStoreRejectsUnsafeKeys(string key)
    {
        IFluNetBlobStore store = new InMemoryFluNetBlobStore();

        Assert.ThrowsAsync<ArgumentException>(() => store.PutAsync(key, "value").AsTask());
    }

    [Test]
    public void BlobCapabilityExposesPortableStoragePermissions()
    {
        BlobStorageCapabilityProvider provider = new();

        Assert.That(provider.IsAvailable, Is.True);
        Assert.That(provider.Descriptor.Id, Is.EqualTo("storage.blob"));
        Assert.That(provider.Descriptor.Permissions, Does.Contain("storage.blob.read"));
        Assert.That(provider.Descriptor.Permissions, Does.Contain("storage.blob.write"));
        Assert.That(provider.Descriptor.Permissions, Does.Contain("storage.blob.delete"));
    }

    [Test]
    public async Task HttpBlobStoreUsesRelativeKeyObjectContract()
    {
        BlobHandler handler = new();
        using HttpClient client = new(handler);
        IFluNetBlobStore store = new HttpFluNetBlobStore(
            new Uri("https://objects.example.test/bucket/"),
            client,
            new AllowAllExecutionPolicy());

        await store.PutAsync("reports/latest", "hello");

        Assert.That(await store.GetAsync("reports/latest"), Is.EqualTo("hello"));
        Assert.That(await store.DeleteAsync("reports/latest"), Is.True);
        Assert.That(await store.DeleteAsync("reports/latest"), Is.False);
        Assert.That(handler.Requests, Is.EqualTo(new[] { "PUT /bucket/reports/latest", "GET /bucket/reports/latest", "DELETE /bucket/reports/latest", "DELETE /bucket/reports/latest" }));
    }

    [Test]
    public async Task HttpBlobStoreListsKeysThroughGatewayPrefixContract()
    {
        BlobHandler handler = new();
        using HttpClient client = new(handler);
        IFluNetBlobStore store = new HttpFluNetBlobStore(
            new Uri("https://objects.example.test/bucket/"),
            client,
            new AllowAllExecutionPolicy());

        IReadOnlyList<string> keys = await store.ListAsync("reports/");

        Assert.That(keys, Is.EqualTo(new[] { "reports/latest.json", "reports/old.txt" }));
        Assert.That(handler.Requests.Last(), Is.EqualTo("GET /bucket/?prefix=reports%2F"));
    }

    [Test]
    public async Task HttpBlobStoreCanApplyHostOwnedBearerCredential()
    {
        BlobHandler handler = new();
        using HttpClient client = new(handler);
        SecretValue credential = SecretValue.Create("token-123");
        IFluNetBlobStore store = new HttpFluNetBlobStore(
            new Uri("https://objects.example.test/bucket/"),
            client,
            new AllowAllExecutionPolicy(),
            credential);

        await store.PutAsync("reports/latest", "hello");

        Assert.That(handler.AuthorizationHeaders.Single(), Is.EqualTo("Bearer token-123"));
    }

    [Test]
    public void HttpBlobStoreRejectsNonHttpBaseUri()
    {
        using HttpClient client = new();

        Assert.Throws<ArgumentException>(() => new HttpFluNetBlobStore(
            new Uri("file:///tmp/blobs"), client, new AllowAllExecutionPolicy()));
    }

    [Test]
    public async Task S3BlobStoreUsesSignatureV4AndPortableObjectOperations()
    {
        S3Handler handler = new();
        using HttpClient client = new(handler);
        S3FluNetBlobStore store = new(
            new Uri("https://s3.example.test/"),
            "demo",
            "us-east-1",
            client,
            new AllowAllExecutionPolicy(),
            new S3FluNetCredentials(SecretValue.Create("access"), SecretValue.Create("secret"), SecretValue.Create("session")));

        await store.PutAsync("reports/latest", "hello");
        Assert.That(await store.GetAsync("reports/latest"), Is.EqualTo("hello"));
        Assert.That(await store.ListAsync("reports/"), Is.EqualTo(new[] { "reports/latest" }));
        Assert.That(await store.DeleteAsync("reports/latest"), Is.True);
        Assert.That(handler.AuthorizationHeaders, Is.Not.Empty);
        Assert.That(handler.AuthorizationHeaders.All(value => value.StartsWith("AWS4-HMAC-SHA256", StringComparison.Ordinal)), Is.True);
    }

    private sealed class S3Handler : HttpMessageHandler
    {
        private readonly Dictionary<string, string> values = new(StringComparer.Ordinal);
        public List<string> AuthorizationHeaders { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AuthorizationHeaders.Add(request.Headers.GetValues("Authorization").Single());
            string key = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Put)
            {
                values[key] = await request.Content!.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            if (request.Method == HttpMethod.Get && request.RequestUri.Query.Contains("list-type=2", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<ListBucketResult><Contents><Key>reports/latest</Key></Contents></ListBucketResult>") };
            if (request.Method == HttpMethod.Get)
                return values.TryGetValue(key, out string? value)
                    ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(value) }
                    : new HttpResponseMessage(HttpStatusCode.NotFound);
            if (request.Method == HttpMethod.Delete)
                return values.Remove(key) ? new HttpResponseMessage(HttpStatusCode.NoContent) : new HttpResponseMessage(HttpStatusCode.NotFound);
            return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
        }
    }

    private sealed class BlobHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, string> values = new(StringComparer.Ordinal);
        public List<string> Requests { get; } = [];
        public List<string> AuthorizationHeaders { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string key = request.RequestUri!.AbsolutePath;
            Requests.Add($"{request.Method} {key}{request.RequestUri.Query}");
            if (request.Headers.Authorization is not null)
                AuthorizationHeaders.Add(request.Headers.Authorization.ToString());
            if (request.Method == HttpMethod.Put)
            {
                values[key] = await request.Content!.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            if (request.Method == HttpMethod.Get)
            {
                if (request.RequestUri!.AbsolutePath == "/bucket/")
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("[\"reports/latest.json\",\"reports/old.txt\"]", System.Text.Encoding.UTF8, "application/json")
                    };
                }
                return values.TryGetValue(key, out string? value)
                    ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(value) }
                    : new HttpResponseMessage(HttpStatusCode.NotFound);
            }
            if (request.Method == HttpMethod.Delete)
            {
                return values.Remove(key)
                    ? new HttpResponseMessage(HttpStatusCode.NoContent)
                    : new HttpResponseMessage(HttpStatusCode.NotFound);
            }
            return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
        }
    }
}
