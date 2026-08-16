using FluNET.Capabilities;
using System.Net;
using System.Net.Http.Headers;

namespace FluNET.Tests.Capabilities;

[TestFixture]
public sealed class SecureHostCapabilityTests
{
    [Test]
    public void ApiKeyAndBasicSchemesApplyOnlyTheirExpectedHeaders()
    {
        using HttpRequestMessage apiRequest = new(HttpMethod.Get, "https://api.example.test");
        new ApiKeyHttpAuthenticationScheme("X-Developer-Key").Apply(apiRequest, SecretValue.Create("secret-key"));

        using HttpRequestMessage basicRequest = new(HttpMethod.Get, "https://api.example.test");
        new BasicHttpAuthenticationScheme().Apply(basicRequest, SecretValue.Create("alice:password"));

        Assert.Multiple(() =>
        {
            Assert.That(apiRequest.Headers.GetValues("X-Developer-Key"), Is.EqualTo(new[] { "secret-key" }));
            Assert.That(apiRequest.Headers.Authorization, Is.Null);
            Assert.That(basicRequest.Headers.Authorization, Is.EqualTo(new AuthenticationHeaderValue("Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("alice:password")))));
        });
    }

    [Test]
    public void ApiKeySchemeRejectsInvalidHeaderNames()
    {
        Assert.Throws<ArgumentException>(() => new ApiKeyHttpAuthenticationScheme("X Bad"));
    }

    [Test]
    public void EnvironmentAndCompositeSecretStoresResolveWithoutChangingOpaqueValues()
    {
        string name = "TEST_" + Guid.NewGuid().ToString("N");
        string variable = "FLUNET_SECRET_" + name;
        Environment.SetEnvironmentVariable(variable, "environment-secret");
        try
        {
            EnvironmentSecretStore environment = new();
            CompositeSecretStore composite = new([
                new DictionarySecretStore(new Dictionary<string, string> { ["fallback"] = "dictionary-secret" }),
                environment]);

            Assert.Multiple(() =>
            {
                Assert.That(composite.TryGet(name, out SecretValue? fromEnvironment), Is.True);
                Assert.That(fromEnvironment!.ToString(), Is.EqualTo("<secret>"));
                Assert.That(fromEnvironment.Reveal(), Is.EqualTo("environment-secret"));
                Assert.That(composite.TryGet("fallback", out SecretValue? fromDictionary), Is.True);
                Assert.That(fromDictionary!.ToString(), Is.EqualTo("<secret>"));
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

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
    public void Ipv4MappedLoopbackAndCarrierGradeNatAreDenied()
    {
        string root = Path.GetTempPath();
        SecureExecutionPolicy policy = new(new SecureHostOptions(
            [root],
            ["::ffff:127.0.0.1", "100.64.0.1"]));

        Assert.Multiple(() =>
        {
            Assert.Throws<CapabilityDeniedException>(() =>
                policy.EnsureAddressAccess("mapped-loopback", IPAddress.Parse("::ffff:127.0.0.1")));
            Assert.Throws<CapabilityDeniedException>(() =>
                policy.EnsureNetworkAccess(new Uri("https://100.64.0.1/")));
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
    public void RedirectToPrivateAddressIsRevalidatedBeforeSecondRequest()
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
