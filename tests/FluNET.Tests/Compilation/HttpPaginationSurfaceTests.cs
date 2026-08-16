using FluNET.Capabilities;
using FluNET.Compilation;
using FluNET.Context;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;
using System.Text.Json;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class HttpPaginationSurfaceTests
{
    [Test]
    public async Task PaginateCollectsItemsAcrossRelativeNextLinks()
    {
        FakeHttpTransport transport = new();
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(services =>
            services.AddSingleton<IHttpTransport>(transport));

        SurfaceCompilationResult compilation = context.CompileSurface(
            "PAGINATE \"https://api.example.test/items\" ITEMS \"items\" NEXT \"next\" LIMIT 5 AS allItems");

        Assert.That(compilation.IsValid, Is.True,
            string.Join(" | ", compilation.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        Assert.That(compilation.Lowering.CanonicalSyntax.Commands.Single().AllTokens.First().Text, Is.EqualTo("PAGINATEJSON"));

        SurfaceExecutionResult execution = await context.ExecuteSurfaceAsync(
            "PAGINATE \"https://api.example.test/items\" ITEMS \"items\" NEXT \"next\" LIMIT 5 AS allItems");

        Assert.That(execution.IsSuccess, Is.True, execution.Error?.ToString());
        JsonElement[] items = (JsonElement[])execution.Result!;
        Assert.That(items.Select(item => item.GetProperty("id").GetInt32()), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(transport.Requests, Is.EqualTo(new[] { "https://api.example.test/items", "https://api.example.test/page-2" }));
    }

    [Test]
    public void PaginationRejectsAnUnsafePageLimitDuringLowering()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();

        SurfaceCompilationResult compilation = context.CompileSurface(
            "PAGINATE \"https://api.example.test/items\" ITEMS \"items\" NEXT \"next\" LIMIT 1001 AS allItems");

        Assert.That(compilation.IsValid, Is.False);
        Assert.That(compilation.Lowering.Diagnostics.Any(item => item.Code == "FLN358"), Is.True,
            string.Join(" | ", compilation.Lowering.Diagnostics.Select(item => item.Code + ":" + item.Message)));
    }

    private sealed class FakeHttpTransport : IHttpTransport
    {
        public List<string> Requests { get; } = [];

        public Task<HttpResourceResponse> GetAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(uri.ToString());
            string json = uri.AbsolutePath.EndsWith("page-2", StringComparison.Ordinal)
                ? "{\"items\":[{\"id\":2}],\"next\":null}"
                : "{\"items\":[{\"id\":1}],\"next\":\"/page-2\"}";
            return Task.FromResult(new HttpResourceResponse(
                Encoding.UTF8.GetBytes(json),
                (int)HttpStatusCode.OK,
                "application/json",
                "utf-8",
                new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)));
        }

        public Task<byte[]> GetBytesAsync(Uri uri, CancellationToken cancellationToken = default) =>
            GetAsync(uri, cancellationToken).ContinueWith(task => task.Result.Content, cancellationToken);

        public Task<string> PostJsonAsync(Uri uri, string json, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
