using FluNET.Capabilities;
using System.Net;

namespace FluNET.Tests.Capabilities;

[TestFixture]
public sealed class SecureHostCapabilityTests
{
    [Test]
    public void PlainHttpAndPrivateLiteralAreDeniedByDefault()
    {
        string root = Path.GetTempPath();
        SecureExecutionPolicy policy = new(new SecureHostOptions([root], ["93.184.216.34", "127.0.0.1"]));
        Assert.Multiple(() =>
        {
            Assert.Throws<CapabilityDeniedException>(() => policy.EnsureNetworkAccess(new Uri("http://93.184.216.34/")));
            Assert.Throws<CapabilityDeniedException>(() => policy.EnsureNetworkAccess(new Uri("https://127.0.0.1/")));
        });
    }

    [Test]
    public void ParentTraversalOutsideRootIsDenied()
    {
        string root = Path.Combine(Path.GetTempPath(), "FluNET_Secure_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            SecureExecutionPolicy policy = new(new SecureHostOptions([root], Array.Empty<string>()));
            Assert.Throws<CapabilityDeniedException>(() => policy.EnsureFileAccess(Path.Combine(root, "..", "escape.txt")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Test]
    public async Task RedirectToPrivateAddressIsRevalidatedBeforeSecondRequest()
    {
        SecureExecutionPolicy policy = new(new SecureHostOptions(
            [Path.GetTempPath()],
            ["93.184.216.34", "127.0.0.1"]));
        RedirectHandler handler = new();
        using SecureHttpTransport transport = new(policy, new BearerHttpAuthenticationScheme(), handler);

        Assert.ThrowsAsync<CapabilityDeniedException>(async () =>
            await transport.GetAsync(new Uri("https://93.184.216.34/start")));
        Assert.That(handler.Requests, Is.EqualTo(1));
    }

    private sealed class RedirectHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            HttpResponseMessage response = new(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri("https://127.0.0.1/private");
            return Task.FromResult(response);
        }
    }
}
