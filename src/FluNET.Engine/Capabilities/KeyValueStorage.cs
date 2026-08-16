using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace FluNET.Capabilities;

public interface IFluNetKeyValueStore
{
    ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default);
    ValueTask SetAsync(string key, string value, CancellationToken cancellationToken = default);
    ValueTask<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<KeyValuePair<string, string>>> ListAsync(string prefix = "", CancellationToken cancellationToken = default);
}

public sealed class InMemoryFluNetKeyValueStore : IFluNetKeyValueStore
{
    private readonly ConcurrentDictionary<string, string> values = new(StringComparer.Ordinal);

    public ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        values.TryGetValue(key, out string? value);
        return ValueTask.FromResult(value);
    }

    public ValueTask SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        values[key] = value;
        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<KeyValuePair<string, string>>> ListAsync(string prefix = "", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IReadOnlyList<KeyValuePair<string, string>>>(values
            .Where(pair => pair.Key.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToArray());
    }

    public ValueTask<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(values.TryRemove(key, out _));
    }
}

public sealed class JsonFileFluNetKeyValueStore : IFluNetKeyValueStore
{
    private readonly string path;
    private readonly IExecutionPolicy policy;
    private readonly SemaphoreSlim gate = new(1, 1);

    public JsonFileFluNetKeyValueStore(string path, IExecutionPolicy policy)
    {
        this.path = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public async ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(path)) return null;
            policy.EnsureFileAccess(path);
            Dictionary<string, string>? values = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false));
            return values is not null && values.TryGetValue(key, out string? value) ? value : null;
        }
        finally { gate.Release(); }
    }

    public async ValueTask SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            policy.EnsureFileAccess(path);
            Dictionary<string, string> values = await ReadValuesAsync(cancellationToken).ConfigureAwait(false);
            values[key] = value;
            string directory = Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory();
            Directory.CreateDirectory(directory);

            // Never expose a partially written JSON document to another reader.
            string temporaryPath = Path.Combine(
                directory,
                $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            try
            {
                await File.WriteAllTextAsync(
                    temporaryPath,
                    JsonSerializer.Serialize(values),
                    cancellationToken).ConfigureAwait(false);
                File.Move(temporaryPath, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }
        finally { gate.Release(); }
    }

    public async ValueTask<IReadOnlyList<KeyValuePair<string, string>>> ListAsync(string prefix = "", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        cancellationToken.ThrowIfCancellationRequested();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(path)) return [];
            policy.EnsureFileAccess(path);
            Dictionary<string, string> values = await ReadValuesAsync(cancellationToken).ConfigureAwait(false);
            return values
                .Where(pair => pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToArray();
        }
        finally { gate.Release(); }
    }

    public async ValueTask<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(path)) return false;
            policy.EnsureFileAccess(path);
            Dictionary<string, string> values = await ReadValuesAsync(cancellationToken).ConfigureAwait(false);
            if (!values.Remove(key)) return false;
            await WriteValuesAsync(values, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally { gate.Release(); }
    }

    private async ValueTask<Dictionary<string, string>> ReadValuesAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return [];
        string json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
    }

    private async ValueTask WriteValuesAsync(Dictionary<string, string> values, CancellationToken cancellationToken)
    {
        string directory = Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(values), cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}

public sealed class SqliteFluNetKeyValueStore : IFluNetKeyValueStore
{
    private readonly string path;
    private readonly IExecutionPolicy policy;
    private readonly SemaphoreSlim gate = new(1, 1);

    public SqliteFluNetKeyValueStore(string path, IExecutionPolicy policy)
    {
        this.path = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public async ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using SqliteConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT value FROM flunet_key_values WHERE key = $key;";
            command.Parameters.AddWithValue("$key", key);
            return (string?)await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public async ValueTask SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using SqliteConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO flunet_key_values(key, value) VALUES ($key, $value)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                """;
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$value", value);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public async ValueTask<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using SqliteConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM flunet_key_values WHERE key = $key;";
            command.Parameters.AddWithValue("$key", key);
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
        }
        finally { gate.Release(); }
    }

    public async ValueTask<IReadOnlyList<KeyValuePair<string, string>>> ListAsync(string prefix = "", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using SqliteConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT key, value FROM flunet_key_values WHERE key LIKE $prefix || '%' ORDER BY key;";
            command.Parameters.AddWithValue("$prefix", prefix);
            List<KeyValuePair<string, string>> values = [];
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                values.Add(new(reader.GetString(0), reader.GetString(1)));
            return values;
        }
        finally { gate.Release(); }
    }

    private async ValueTask<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        policy.EnsureFileAccess(path);
        string directory = Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(directory);
        SqliteConnection connection = new(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS flunet_key_values (key TEXT PRIMARY KEY, value TEXT NOT NULL);";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}

public sealed class KeyValueStorageCapabilityProvider : ICapabilityProvider
{
    public CapabilityDescriptor Descriptor { get; } = new(
        "storage.keyvalue",
        "1.0",
        [FluNetPlatform.Any],
        ["storage.read", "storage.write"]);

    public bool IsAvailable => true;
}
