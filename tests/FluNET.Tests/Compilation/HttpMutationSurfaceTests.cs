using FluNET.Capabilities;
using FluNET.Compilation;
using FluNET.Context;
using FluNET.Tooling;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class HttpMutationSurfaceTests
{
    [Test]
    public async Task PutPatchAndDeleteUseTheNetworkCapability()
    {
        CaptureHttp http = new();
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(
            services => services.AddSingleton<IHttpTransport>(http));

        SurfaceExecutionResult put = await context.ExecuteSurfaceAsync(
            "PUT \"new value\" TO https://api.example.test/items/1");
        SurfaceExecutionResult patch = await context.ExecuteSurfaceAsync(
            "PATCH \"partial value\" TO https://api.example.test/items/1");
        SurfaceExecutionResult delete = await context.ExecuteSurfaceAsync(
            "DELETE https://api.example.test/items/1 AS removed");

        Assert.Multiple(() =>
        {
            Assert.That(put.IsSuccess, Is.True, put.Error?.ToString());
            Assert.That(patch.IsSuccess, Is.True, patch.Error?.ToString());
            Assert.That(delete.IsSuccess, Is.True, delete.Error?.ToString());
            Assert.That(http.Methods, Is.EqualTo(new[] { "PUT", "PATCH", "DELETE" }));
            Assert.That(http.Bodies, Is.EqualTo(new[] { "new value", "partial value" }));
        });
    }

    [Test]
    public void HttpMutationFramesAppearInTheCapabilityGraph()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        SurfaceCompilationResult compilation = context.CompileSurface(
            "PUT \"body\" TO https://api.example.test/items/1\nDELETE https://api.example.test/items/1 AS removed");

        Assert.That(compilation.IsValid, Is.True,
            string.Join(" | ", compilation.Diagnostics.Select(item => item.Message)));
        string graph = new SurfaceGraphExporter().ToDot(compilation);

        Assert.That(graph, Does.Contain("core.put.json"));
        Assert.That(graph, Does.Contain("core.delete.http"));
        Assert.That(graph, Does.Contain("network.http"));
    }

    [Test]
    public async Task AuthDirectiveAppliesSecretToHttpMutation()
    {
        CaptureAuthenticatedHttp authenticated = new();
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(
            services =>
            {
                services.AddSingleton<IHttpTransport>(new CaptureHttp());
                services.AddSingleton<IAuthenticatedHttpTransport>(authenticated);
                services.AddSingleton<ISecretStore>(new DictionarySecretStore(
                    new Dictionary<string, string> { ["token"] = "opaque-token" }));
                services.AddSingleton<ISecretAccessPolicy>(new AllowListedSecretAccessPolicy(["token"]));
            });

        SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(
            "AUTH secret:token\nPUT \"body\" TO https://api.example.test/items/1");

        Assert.That(execution.IsSuccess, Is.True,
            execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Diagnostics.Select(item => item.Message)));
        Assert.That(authenticated.Credential, Is.EqualTo("opaque-token"));
        Assert.That(authenticated.Method, Is.EqualTo("PUT"));
    }

    private sealed class CaptureHttp : IHttpTransport
    {
        public List<string> Methods { get; } = [];
        public List<string> Bodies { get; } = [];

        public Task<byte[]> GetBytesAsync(Uri uri, CancellationToken cancellationToken = default) =>
            Task.FromResult(Array.Empty<byte>());

        public Task<HttpResourceResponse> GetAsync(Uri uri, CancellationToken cancellationToken = default) =>
            Task.FromResult(new HttpResourceResponse([], 200, "text/plain", "utf-8", new Dictionary<string, string[]>()));

        public Task<string> PostJsonAsync(Uri uri, string json, CancellationToken cancellationToken = default) =>
            Task.FromResult("post");

        public Task<string> PutJsonAsync(Uri uri, string json, CancellationToken cancellationToken = default)
        {
            Methods.Add("PUT");
            Bodies.Add(json);
            return Task.FromResult("put");
        }

        public Task<string> PatchJsonAsync(Uri uri, string json, CancellationToken cancellationToken = default)
        {
            Methods.Add("PATCH");
            Bodies.Add(json);
            return Task.FromResult("patch");
        }

        public Task<string> DeleteAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            Methods.Add("DELETE");
            return Task.FromResult("deleted");
        }
    }

    private sealed class CaptureAuthenticatedHttp : IAuthenticatedHttpTransport
    {
        public string? Credential { get; private set; }
        public string? Method { get; private set; }

        public Task<HttpResourceResponse> GetAsync(Uri uri, SecretValue credential, CancellationToken cancellationToken = default) =>
            Task.FromResult(new HttpResourceResponse([], 200, "text/plain", "utf-8", new Dictionary<string, string[]>()));

        public Task<string> PutJsonAsync(Uri uri, string json, SecretValue credential, CancellationToken cancellationToken = default)
        {
            Credential = credential.Reveal();
            Method = "PUT";
            return Task.FromResult("authorized");
        }

        public Task<string> PostJsonAsync(Uri uri, string json, SecretValue credential, CancellationToken cancellationToken = default) =>
            Task.FromResult("post");

        public Task<string> PatchJsonAsync(Uri uri, string json, SecretValue credential, CancellationToken cancellationToken = default) =>
            Task.FromResult("patch");

        public Task<string> DeleteAsync(Uri uri, SecretValue credential, CancellationToken cancellationToken = default) =>
            Task.FromResult("deleted");
    }
}
