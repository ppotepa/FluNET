using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace FluNET.Capabilities;

public interface ISqlQueryExecutor
{
    ValueTask<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
        string query,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
        string query,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default);

    ValueTask<int> ExecuteAsync(
        string query,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default);
}

/// <summary>Default host boundary: SQL is denied until an executor/connection is explicitly configured.</summary>
public sealed class DenySqlQueryExecutor : ISqlQueryExecutor
{
    public ValueTask<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(string query, CancellationToken cancellationToken = default) =>
        throw new CapabilityDeniedException("SQL access is not configured for this FluNET host.");

    public ValueTask<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
        string query,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default) =>
        throw new CapabilityDeniedException("SQL access is not configured for this FluNET host.");

    public ValueTask<int> ExecuteAsync(string query, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken = default) =>
        throw new CapabilityDeniedException("SQL access is not configured for this FluNET host.");
}

/// <summary>Provider-neutral ADO.NET adapter. Hosts own connection creation, credentials and pooling.</summary>
public sealed class DbConnectionSqlQueryExecutor(
    Func<CancellationToken, ValueTask<DbConnection>> connectionFactory) : ISqlQueryExecutor
{
    public async ValueTask<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
        string query,
        CancellationToken cancellationToken = default)
        => await QueryAsync(query, new Dictionary<string, object?>(), cancellationToken).ConfigureAwait(false);

    public async ValueTask<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
        string query,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        await using DbConnection connection = await connectionFactory(cancellationToken).ConfigureAwait(false);
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = query;
        AddParameters(command, parameters);
        await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        List<IReadOnlyDictionary<string, object?>> rows = [];
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            Dictionary<string, object?> row = new(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < reader.FieldCount; index++)
            {
                object value = reader.GetValue(index);
                row[reader.GetName(index)] = value is DBNull ? null : value;
            }
            rows.Add(row);
        }
        return rows;
    }

    public async ValueTask<int> ExecuteAsync(
        string query,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        await using DbConnection connection = await connectionFactory(cancellationToken).ConfigureAwait(false);
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = query;
        AddParameters(command, parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AddParameters(DbCommand command, IReadOnlyDictionary<string, object?> parameters)
    {
        foreach (KeyValuePair<string, object?> parameter in parameters)
        {
            DbParameter dbParameter = command.CreateParameter();
            dbParameter.ParameterName = parameter.Key.StartsWith('$') ? parameter.Key : "$" + parameter.Key;
            dbParameter.Value = parameter.Value ?? DBNull.Value;
            command.Parameters.Add(dbParameter);
        }
    }
}

/// <summary>
/// Convenience adapter for any host-registered ADO.NET provider. The engine
/// references only <see cref="DbProviderFactory"/>; providers such as
/// PostgreSQL, MySQL and SQL Server remain optional host dependencies.
/// </summary>
public sealed class DbProviderFactorySqlQueryExecutor : ISqlQueryExecutor
{
    private readonly DbConnectionSqlQueryExecutor inner;

    public DbProviderFactorySqlQueryExecutor(DbProviderFactory provider, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        inner = new(_ =>
        {
            DbConnection connection = provider.CreateConnection()
                ?? throw new InvalidOperationException("The configured ADO.NET provider could not create a connection.");
            connection.ConnectionString = connectionString;
            return ValueTask.FromResult(connection);
        });
    }

    public ValueTask<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
        string query, CancellationToken cancellationToken = default) => inner.QueryAsync(query, cancellationToken);

    public ValueTask<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
        string query,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default) => inner.QueryAsync(query, parameters, cancellationToken);

    public ValueTask<int> ExecuteAsync(
        string query,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default) => inner.ExecuteAsync(query, parameters, cancellationToken);
}

/// <summary>
/// Portable SQLite provider for local, single-host workflows. The database file
/// remains under the host execution policy and the provider exposes the same
/// provider-neutral query boundary as any external database adapter.
/// </summary>
public sealed class SqliteFluNetQueryExecutor : ISqlQueryExecutor
{
    private readonly string path;
    private readonly IExecutionPolicy policy;

    public SqliteFluNetQueryExecutor(string path, IExecutionPolicy policy)
    {
        this.path = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public async ValueTask<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
        string query,
        CancellationToken cancellationToken = default)
        => await QueryAsync(query, new Dictionary<string, object?>(), cancellationToken).ConfigureAwait(false);

    public async ValueTask<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
        string query,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        policy.EnsureFileAccess(path);
        return await new DbConnectionSqlQueryExecutor(
            _ => ValueTask.FromResult<DbConnection>(new SqliteConnection($"Data Source={path};Pooling=False")))
            .QueryAsync(query, parameters, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<int> ExecuteAsync(
        string query,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        policy.EnsureFileAccess(path);
        return await new DbConnectionSqlQueryExecutor(
            _ => ValueTask.FromResult<DbConnection>(new SqliteConnection($"Data Source={path};Pooling=False")))
            .ExecuteAsync(query, parameters, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        policy.EnsureFileAccess(path);
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        await using SqliteConnection connection = new($"Data Source={path};Pooling=False");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class SqlQueryCapabilityProvider(ISqlQueryExecutor executor) : ICapabilityProvider
{
    public CapabilityDescriptor Descriptor { get; } = new(
        "database.sql",
        "1.0",
        [FluNetPlatform.Any],
        ["database.read", "database.write"]);

    public bool IsAvailable => executor is not DenySqlQueryExecutor;
}
