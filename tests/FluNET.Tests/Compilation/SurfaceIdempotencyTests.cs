using FluNET.Capabilities;
using FluNET.Context;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class SurfaceIdempotencyTests
{
    [Test]
    public async Task OnceBySuppressesRepeatedMutationForSameKey()
    {
        CountingHttp transport = new();
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(services => services.AddSingleton<IHttpTransport>(transport));
        using JsonDocument order = JsonDocument.Parse("{\"id\":7,\"name\":\"book\"}");
        context.GetEngine().RegisterVariable("order", order.RootElement.Clone());
        const string source = "POST order TO https://api.example.test/orders ONCE BY order.id";
        SurfaceExecutionResult first = await context.ExecuteSurfaceAsync(source);
        SurfaceExecutionResult second = await context.ExecuteSurfaceAsync(source);
        Assert.Multiple(() =>
        {
            Assert.That(first.IsSuccess, Is.True, first.Error?.Message);
            Assert.That(second.IsSuccess, Is.True, second.Error?.Message);
            Assert.That(transport.PostCalls, Is.EqualTo(1));
        });
    }

    private sealed class CountingHttp : IHttpTransport
    {
        public int PostCalls { get; private set; }
        public Task<byte[]> GetBytesAsync(Uri uri, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> PostJsonAsync(Uri uri, string json, CancellationToken cancellationToken = default) { PostCalls++; return Task.FromResult("ok"); }
    }
}
