using System.Text.Json;
using System.Text;
using FluNET.Capabilities;
using FluNET.Context;
using FluNET.Execution;
using FluNET.Language;
using FluNET.Prompt;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Tests.Compilation;

public sealed class FileMetadataIndexSurfaceTests
{
    [Test]
    public async Task CompactIndexAcceptsAQuotedRootPath()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-index-quoted-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "sample.txt"), "sample");
        try
        {
            using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(services =>
                services.AddSingleton<IFluNetFileMetadataIndex>(new PhysicalFluNetFileMetadataIndex(new AllowAllExecutionPolicy())));

            var execution = await context.ExecuteSurfaceAsync($"INDEX FILES files FROM \"{root}\" RECURSIVE");

            Assert.That(execution.IsSuccess, Is.True, execution.Error?.ToString());
            Assert.That(execution.Result, Is.InstanceOf<JsonElement[]>());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task Index_Files_ReturnsPortableMetadata()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-index-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string file = Path.Combine(root, "sample.json");
        await File.WriteAllTextAsync(file, "{}\n");
        try
        {
            using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(services =>
                services.AddSingleton<IFluNetFileMetadataIndex>(new PhysicalFluNetFileMetadataIndex(new AllowAllExecutionPolicy())));
            ExecutionResult result = await context.GetEngine().ExecuteAsync(new ProcessedPrompt($"INDEX FILES [files] FROM {{{root}}}"));

            Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
            JsonElement[] rows = (JsonElement[])result.Result!;
            Assert.That(rows, Has.Length.EqualTo(1));
            Assert.That(rows[0].GetProperty("name").GetString(), Is.EqualTo("sample.json"));
            Assert.That(rows[0].GetProperty("extension").GetString(), Is.EqualTo(".json"));
            Assert.That(rows[0].GetProperty("length").GetInt64(), Is.GreaterThan(0));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task SqliteIndex_CanBeReopenedAndReadWithoutRescan()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-index-" + Guid.NewGuid().ToString("N"));
        string database = Path.Combine(root, "index.db");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "first.txt"), "one");
        try
        {
            SqliteFluNetFileMetadataIndex writer = new(database, new AllowAllExecutionPolicy());
            IReadOnlyList<FluNetFileIndexEntry> written = await writer.RebuildAsync(root, recursive: false);
            Assert.That(written, Has.Count.EqualTo(1));

            SqliteFluNetFileMetadataIndex reader = new(database, new AllowAllExecutionPolicy());
            IReadOnlyList<FluNetFileIndexEntry> indexed = await reader.QueryAsync(root);
            Assert.That(indexed, Has.Count.EqualTo(1));
            Assert.That(indexed[0].Name, Is.EqualTo("first.txt"));

            IReadOnlyList<FluNetFileIndexEntry> filtered = await reader.QueryAsync(
                root,
                new FluNetFileIndexQuery("extension == '.txt'", "name DESC", Take: 1));
            Assert.That(filtered, Has.Count.EqualTo(1));
            Assert.That(filtered[0].Extension, Is.EqualTo(".txt"));

            using FluNETContext context = FluNETContext.Create(services =>
                services.AddSingleton<IFluNetFileMetadataIndex>(new SqliteFluNetFileMetadataIndex(database, new AllowAllExecutionPolicy())));
            ExecutionResult result = await context.GetEngine().ExecuteAsync(new ProcessedPrompt(
                $"READ INDEX [files] FROM {{{root}}} WHERE {{extension == '.txt'}} ORDER BY {{name DESC}} TAKE {{1}}"));
            Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
            Assert.That(((JsonElement[])result.Result!).Length, Is.EqualTo(1));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task HttpIndex_UsesPortableRebuildAndQueryContract()
    {
        RecordingIndexTransport transport = new();
        HttpFluNetFileMetadataIndex index = new(new Uri("https://index.example.test/api/"), transport);

        IReadOnlyList<FluNetFileIndexEntry> rebuilt = await index.RebuildAsync("/workspace", recursive: true);
        Assert.That(rebuilt, Has.Count.EqualTo(1));
        Assert.That(transport.PostedJson, Does.Contain("\"recursive\":true"));

        IReadOnlyList<FluNetFileIndexEntry> queried = await index.QueryAsync(
            "/workspace", new FluNetFileIndexQuery("extension == '.json'", "name DESC", 2, 10));
        Assert.That(queried, Has.Count.EqualTo(1));
        Assert.That(transport.LastQuery, Does.Contain("predicate="));
        Assert.That(transport.LastQuery, Does.Contain("orderBy="));
    }

    private sealed class RecordingIndexTransport : IHttpTransport
    {
        public string? PostedJson { get; private set; }
        public string? LastQuery { get; private set; }

        public Task<byte[]> GetBytesAsync(Uri uri, CancellationToken cancellationToken = default) =>
            Task.FromResult(Encoding.UTF8.GetBytes("[]"));

        public Task<HttpResourceResponse> GetAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            LastQuery = uri.Query;
            string json = "[{\"path\":\"/workspace/data.json\",\"name\":\"data.json\",\"extension\":\".json\",\"length\":4,\"modifiedUtc\":\"2026-01-01T00:00:00Z\",\"createdUtc\":\"2026-01-01T00:00:00Z\",\"isHidden\":false,\"isReadOnly\":false}]";
            return Task.FromResult(new HttpResourceResponse(Encoding.UTF8.GetBytes(json), 200, "application/json", null, new Dictionary<string, string[]>()));
        }

        public Task<string> PostJsonAsync(Uri uri, string json, CancellationToken cancellationToken = default)
        {
            PostedJson = json;
            return Task.FromResult("[{\"path\":\"/workspace/data.json\",\"name\":\"data.json\",\"extension\":\".json\",\"length\":4,\"modifiedUtc\":\"2026-01-01T00:00:00Z\",\"createdUtc\":\"2026-01-01T00:00:00Z\",\"isHidden\":false,\"isReadOnly\":false}]");
        }
    }
}
