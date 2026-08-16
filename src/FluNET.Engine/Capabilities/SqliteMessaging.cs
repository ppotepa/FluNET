using Microsoft.Data.Sqlite;
using System.Data.Common;

namespace FluNET.Capabilities;

public sealed class SqliteFluNetMessageBus : IFluNetMessageBus
{
    private readonly string path;
    private readonly IExecutionPolicy policy;
    private readonly SemaphoreSlim gate = new(1, 1);

    public SqliteFluNetMessageBus(string path, IExecutionPolicy policy)
    {
        this.path = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public async ValueTask PublishAsync(string topic, string payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(payload);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using SqliteConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO flunet_messages(message_id, topic, payload, published_utc) VALUES ($id, $topic, $payload, $published);";
            command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
            command.Parameters.AddWithValue("$topic", topic);
            command.Parameters.AddWithValue("$payload", payload);
            command.Parameters.AddWithValue("$published", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public async ValueTask<FluNetMessage> ReceiveAsync(string topic, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        while (true)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await using SqliteConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
                await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                await using SqliteCommand read = connection.CreateCommand();
                read.Transaction = transaction;
                read.CommandText = "SELECT message_id, topic, payload, published_utc FROM flunet_messages WHERE topic = $topic ORDER BY sequence_id LIMIT 1;";
                read.Parameters.AddWithValue("$topic", topic);
                await using SqliteDataReader reader = await read.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    string id = reader.GetString(0);
                    FluNetMessage message = new(reader.GetString(1), reader.GetString(2), id,
                        DateTimeOffset.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind));
                    await reader.DisposeAsync().ConfigureAwait(false);
                    await using SqliteCommand delete = connection.CreateCommand();
                    delete.Transaction = transaction;
                    delete.CommandText = "DELETE FROM flunet_messages WHERE message_id = $id;";
                    delete.Parameters.AddWithValue("$id", id);
                    await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    return message;
                }
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }
            finally { gate.Release(); }
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
        }
    }

    public async IAsyncEnumerable<FluNetMessage> ReadAsync(
        string topic,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (true) yield return await ReceiveAsync(topic, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        policy.EnsureFileAccess(path);
        string directory = Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(directory);
        SqliteConnection connection = new(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand schema = connection.CreateCommand();
        schema.CommandText = "CREATE TABLE IF NOT EXISTS flunet_messages (sequence_id INTEGER PRIMARY KEY AUTOINCREMENT, message_id TEXT NOT NULL UNIQUE, topic TEXT NOT NULL, payload TEXT NOT NULL, published_utc TEXT NOT NULL); CREATE INDEX IF NOT EXISTS ix_flunet_messages_topic_sequence ON flunet_messages(topic, sequence_id);";
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}
