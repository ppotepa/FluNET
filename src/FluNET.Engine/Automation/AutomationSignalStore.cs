using FluNET.Capabilities;
using Microsoft.Data.Sqlite;
using System.Collections.Concurrent;
using System.Text.Json;

namespace FluNET.Automation;

public sealed record AutomationSignalEnvelope(
    DateTimeOffset Timestamp,
    AutomationSignal Signal);

public interface IAutomationSignalStore
{
    ValueTask AppendAsync(
        AutomationSignalEnvelope envelope,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<AutomationSignalEnvelope>> ReadAsync(
        CancellationToken cancellationToken = default);
}

public sealed class InMemoryAutomationSignalStore : IAutomationSignalStore
{
    private readonly ConcurrentQueue<AutomationSignalEnvelope> signals = new();

    public ValueTask AppendAsync(AutomationSignalEnvelope envelope, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        signals.Enqueue(envelope);
        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<AutomationSignalEnvelope>> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IReadOnlyList<AutomationSignalEnvelope>>(signals.ToArray());
    }
}

/// <summary>Single-host append-only JSONL signal journal.</summary>
public sealed class JsonFileAutomationSignalStore : IAutomationSignalStore
{
    private readonly string path;
    private readonly IExecutionPolicy policy;
    private readonly SemaphoreSlim gate = new(1, 1);

    public JsonFileAutomationSignalStore(string path, IExecutionPolicy policy)
    {
        this.path = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public async ValueTask AppendAsync(AutomationSignalEnvelope envelope, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        policy.EnsureFileAccess(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory());
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(
                path,
                JsonSerializer.Serialize(envelope) + Environment.NewLine,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<IReadOnlyList<AutomationSignalEnvelope>> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        policy.EnsureFileAccess(path);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(path)) return [];
            List<AutomationSignalEnvelope> result = [];
            foreach (string line in await File.ReadAllLinesAsync(path, cancellationToken).ConfigureAwait(false))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                AutomationSignalEnvelope? envelope = JsonSerializer.Deserialize<AutomationSignalEnvelope>(line);
                if (envelope is not null) result.Add(envelope);
            }
            return result;
        }
        finally
        {
            gate.Release();
        }
    }
}

/// <summary>Single-host SQLite-backed append-only signal journal.</summary>
public sealed class SqliteAutomationSignalStore : IAutomationSignalStore
{
    private readonly string path;
    private readonly IExecutionPolicy policy;
    private readonly SemaphoreSlim gate = new(1, 1);

    public SqliteAutomationSignalStore(string path, IExecutionPolicy policy)
    {
        this.path = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public async ValueTask AppendAsync(AutomationSignalEnvelope envelope, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using SqliteConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO flunet_automation_signals
                    (timestamp_utc, resource, event_name, data_json)
                VALUES ($timestamp, $resource, $event, $data)
                """;
            command.Parameters.AddWithValue("$timestamp", envelope.Timestamp.UtcDateTime.ToString("O"));
            command.Parameters.AddWithValue("$resource", envelope.Signal.Resource);
            command.Parameters.AddWithValue("$event", envelope.Signal.EventName);
            command.Parameters.AddWithValue("$data", JsonSerializer.Serialize(envelope.Signal.Data));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<IReadOnlyList<AutomationSignalEnvelope>> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using SqliteConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT timestamp_utc, resource, event_name, data_json
                FROM flunet_automation_signals
                ORDER BY id
                """;
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            List<AutomationSignalEnvelope> result = [];
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                Dictionary<string, object?> data = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                    reader.GetString(3)) ?? [];
                result.Add(new AutomationSignalEnvelope(
                    DateTimeOffset.Parse(reader.GetString(0), System.Globalization.CultureInfo.InvariantCulture),
                    new AutomationSignal(reader.GetString(1), reader.GetString(2), data)));
            }
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    private async ValueTask<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        policy.EnsureFileAccess(path);
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        SqliteConnection connection = new($"Data Source={path};Pooling=False");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static async Task EnsureSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS flunet_automation_signals (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp_utc TEXT NOT NULL,
                resource TEXT NOT NULL,
                event_name TEXT NOT NULL,
                data_json TEXT NOT NULL
            )
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
