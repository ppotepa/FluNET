using FluNET.Capabilities;
using FluNET.Compilation;
using FluNET.Context;
using FluNET.Execution.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class SurfaceCacheTests
{
    [Test]
    public async Task CachePolicyAvoidsRepeatedResourceDispatchInOneContext()
    {
        CountingHttp transport = new();
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(services => services.AddSingleton<IHttpTransport>(transport));
        const string source = "GET https://api.example.test/posts CACHE 1h AS posts";
        SurfaceCompilationResult compiled = context.CompileSurface(source);
        Assert.That(compiled.IsValid, Is.True, Diagnostics(compiled));
        Assert.That(CommandExecutionArtifactStore.TryGetCache(compiled.BoundProgram!.Commands[0], out ExecutionCachePolicy? policy), Is.True);
        Assert.That(policy!.Ttl, Is.EqualTo(TimeSpan.FromHours(1)));
        SurfaceExecutionResult first = await context.ExecuteSurfaceAsync(source);
        SurfaceExecutionResult second = await context.ExecuteSurfaceAsync(source);
        Assert.Multiple(() =>
        {
            Assert.That(first.IsSuccess, Is.True, first.Error?.Message);
            Assert.That(second.IsSuccess, Is.True, second.Error?.Message);
            Assert.That(transport.Calls, Is.EqualTo(1));
        });
    }

    private static string Diagnostics(SurfaceCompilationResult result) => string.Join(" | ", result.SurfaceParse.Diagnostics.Select(item => $"{item.Code}: {item.Message}")) + " " + string.Join(" | ", result.Diagnostics.Select(item => $"{item.Code}: {item.Message}"));

    private sealed class CountingHttp : IHttpTransport
    {
        public int Calls { get; private set; }
        public Task<byte[]> GetBytesAsync(Uri uri, CancellationToken cancellationToken = default) { Calls++; return Task.FromResult(System.Text.Encoding.UTF8.GetBytes("[]")); }
        public Task<string> PostJsonAsync(Uri uri, string json, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
