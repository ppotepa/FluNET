using FluNET.Capabilities;
using FluNET.Context;
using FluNET.Execution.Commands;
using FluNET.Variables;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace FluNET.Tests.Compilation;

[TestFixture]
public sealed class SqliteProviderTests
{
    [Test]
    public async Task SqliteExecutorRunsPortableLocalQueryThroughSurface()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-sqlite-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "data.db");
        try
        {
            using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext(services =>
                services.AddSingleton<ISqlQueryExecutor>(provider =>
                    new SqliteFluNetQueryExecutor(path, provider.GetRequiredService<IExecutionPolicy>())));
            SqliteFluNetQueryExecutor sqlite = (SqliteFluNetQueryExecutor)context.GetService<ISqlQueryExecutor>();
            await sqlite.QueryAsync("CREATE TABLE items (id INTEGER, name TEXT)");
            await sqlite.QueryAsync("INSERT INTO items VALUES (1, 'one')");

            var execution = await context.ExecuteSurfaceAsync(
                "GET sql:\"SELECT id, name FROM items\" AS items");

            Assert.That(execution.IsSuccess, Is.True,
                execution.Error?.ToString() ?? string.Join(" | ", execution.Compilation.Diagnostics.Select(item => item.Message)));
            JsonElement[] rows = (JsonElement[])execution.Result!;
            Assert.That(rows.Single().GetProperty("name").GetString(), Is.EqualTo("one"));
            Assert.That(context.GetService<CapabilityRegistry>().TryResolve("database.sql", out _), Is.True);

            IReadOnlyList<IReadOnlyDictionary<string, object?>> matches = await sqlite.QueryAsync(
                "SELECT id, name FROM items WHERE name = $wanted",
                new Dictionary<string, object?> { ["wanted"] = "one" });
            Assert.That(matches, Has.Count.EqualTo(1));
            Assert.That(matches[0]["id"], Is.EqualTo(1L));

            var surfaceParameterized = await context.ExecuteSurfaceAsync(
                "LET wanted = 'one'\nGET sql:\"SELECT id, name FROM items WHERE name = $wanted\" AS surfaceMatches");
            Assert.That(surfaceParameterized.IsSuccess, Is.True,
                surfaceParameterized.Error?.ToString() ?? string.Join(" | ", surfaceParameterized.Compilation.Diagnostics.Select(item => item.Message)));
            JsonElement[] surfaceMatches = (JsonElement[])surfaceParameterized.Result!;
            Assert.That(surfaceMatches, Has.Length.EqualTo(1));

            var applied = await context.ExecuteSurfaceAsync(
                "APPLY SQL \"UPDATE items SET name = 'updated' WHERE id = 1\" AS changed");
            Assert.That(applied.IsSuccess, Is.True,
                applied.Error?.ToString() ?? string.Join(" | ", applied.Compilation.Diagnostics.Select(item => item.Message)));
            Assert.That(applied.Result, Is.EqualTo(1));

        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task SqlCommandHandlerBindsFluNetVariablesAsParameters()
    {
        CapturingSql sql = new();
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        IVariableResolver variables = context.GetService<IVariableResolver>();
        variables.Register("wanted", "one");
        GetSqlCommandHandler handler = new(variables, sql);

        await handler.HandleAsync(new GetSqlCommand(
            new LiteralExpression<string>("SELECT * FROM items WHERE name = $wanted")));

        Assert.That(sql.Parameters, Is.Not.Null);
        Assert.That(sql.Parameters!["wanted"], Is.EqualTo("one"));
    }

    [Test]
    public async Task DbProviderFactoryAdapterUsesTheHostSelectedAdoNetProvider()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-provider-factory-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "provider.db");
        try
        {
            DbProviderFactorySqlQueryExecutor executor = new(
                Microsoft.Data.Sqlite.SqliteFactory.Instance,
                $"Data Source={path};Pooling=False");

            await executor.ExecuteAsync("CREATE TABLE values_table (value TEXT)", new Dictionary<string, object?>());
            await executor.ExecuteAsync(
                "INSERT INTO values_table(value) VALUES ($value)",
                new Dictionary<string, object?> { ["value"] = "portable" });
            IReadOnlyList<IReadOnlyDictionary<string, object?>> rows = await executor.QueryAsync(
                "SELECT value FROM values_table", CancellationToken.None);

            Assert.That(rows.Single()["value"], Is.EqualTo("portable"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void SurfaceSqlParameterReferencesAppearInTheExecutionPlan()
    {
        using FluNETContext context = SurfaceCompilationExtensions.CreateSurfaceContext();
        var result = context.CompileSurface(
            "LET wanted = 'one'\nGET sql:\"SELECT id FROM items WHERE name = $wanted\" AS matches");

        Assert.That(result.IsValid, Is.True,
            string.Join(" | ", result.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        Assert.That(result.Plan!.Steps.Select(step => step.Command.Frame.Id.Value),
            Is.EqualTo(new[] { "core.set.text", "surface.get.sql" }));
    }

    private sealed class CapturingSql : ISqlQueryExecutor
    {
        public IReadOnlyDictionary<string, object?>? Parameters { get; private set; }

        public ValueTask<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
            string query, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<IReadOnlyDictionary<string, object?>>>([]);

        public ValueTask<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
            string query,
            IReadOnlyDictionary<string, object?> parameters,
            CancellationToken cancellationToken = default)
        {
            Parameters = parameters;
            return ValueTask.FromResult<IReadOnlyList<IReadOnlyDictionary<string, object?>>>([]);
        }

        public ValueTask<int> ExecuteAsync(
            string query,
            IReadOnlyDictionary<string, object?> parameters,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(0);
    }
}
