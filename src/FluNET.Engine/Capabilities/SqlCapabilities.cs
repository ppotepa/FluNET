using System.Data;
using System.Data.Common;

namespace FluNET.Capabilities;

public interface ISqlQueryExecutor
{
    ValueTask<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
        string query,
        CancellationToken cancellationToken = default);
}

/// <summary>Default host boundary: SQL is denied until an executor/connection is explicitly configured.</summary>
public sealed class DenySqlQueryExecutor : ISqlQueryExecutor
{
    public ValueTask<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(string query, CancellationToken cancellationToken = default) =>
        throw new CapabilityDeniedException("SQL access is not configured for this FluNET host.");
}

/// <summary>Provider-neutral ADO.NET adapter. Hosts own connection creation, credentials and pooling.</summary>
public sealed class DbConnectionSqlQueryExecutor(
    Func<CancellationToken, ValueTask<DbConnection>> connectionFactory) : ISqlQueryExecutor
{
    public async ValueTask<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        await using DbConnection connection = await connectionFactory(cancellationToken).ConfigureAwait(false);
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = query;
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
}
