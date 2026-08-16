namespace FluNET.Capabilities;

using Microsoft.Data.Sqlite;
using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;

public sealed record FluNetFileIndexEntry(
    string Path,
    string Name,
    string Extension,
    long Length,
    DateTimeOffset ModifiedUtc,
    DateTimeOffset CreatedUtc,
    bool IsHidden,
    bool IsReadOnly);

public sealed record FluNetFileIndexQuery(
    string? Predicate = null,
    string? OrderBy = null,
    int Skip = 0,
    int? Take = null)
{
    public void Validate()
    {
        if (Skip < 0) throw new ArgumentOutOfRangeException(nameof(Skip));
        if (Take is < 0) throw new ArgumentOutOfRangeException(nameof(Take));
    }
}

/// <summary>Provider-neutral metadata catalog for local or remote file providers.</summary>
public interface IFluNetFileMetadataIndex
{
    ValueTask<IReadOnlyList<FluNetFileIndexEntry>> RebuildAsync(
        string root,
        bool recursive = true,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<FluNetFileIndexEntry>> QueryAsync(
        string root,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<FluNetFileIndexEntry>> QueryAsync(
        string root,
        FluNetFileIndexQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        query.Validate();
        return QueryAsync(root, cancellationToken);
    }

    ValueTask<IReadOnlyList<FluNetFileIndexEntry>> ApplyChangeAsync(
        string root,
        FluNetFileChange change,
        bool recursive = true,
        CancellationToken cancellationToken = default) =>
        RebuildAsync(root, recursive, cancellationToken);
}

public sealed class PhysicalFluNetFileMetadataIndex(IExecutionPolicy policy) : IFluNetFileMetadataIndex
{
    private readonly object sync = new();
    private readonly Dictionary<string, IReadOnlyList<FluNetFileIndexEntry>> snapshots = new(StringComparer.OrdinalIgnoreCase);

    public ValueTask<IReadOnlyList<FluNetFileIndexEntry>> RebuildAsync(
        string root,
        bool recursive = true,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fullRoot = Path.GetFullPath(root);
        policy.EnsureFileAccess(fullRoot);
        if (!Directory.Exists(fullRoot))
            throw new DirectoryNotFoundException(fullRoot);

        EnumerationOptions options = new()
        {
            RecurseSubdirectories = recursive,
            IgnoreInaccessible = false,
            ReturnSpecialDirectories = false
        };
        List<FluNetFileIndexEntry> entries = [];
        foreach (string path in Directory.EnumerateFiles(fullRoot, "*", options).Order(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            policy.EnsureFileAccess(path);
            FileInfo file = new(path);
            entries.Add(new(
                file.FullName,
                file.Name,
                file.Extension,
                file.Length,
                file.LastWriteTimeUtc,
                file.CreationTimeUtc,
                file.Name.StartsWith(".", StringComparison.Ordinal) || file.Attributes.HasFlag(FileAttributes.Hidden),
                file.IsReadOnly));
        }
        IReadOnlyList<FluNetFileIndexEntry> snapshot = entries;
        lock (sync) snapshots[fullRoot] = snapshot;
        return ValueTask.FromResult(snapshot);
    }

    public ValueTask<IReadOnlyList<FluNetFileIndexEntry>> QueryAsync(string root, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fullRoot = Path.GetFullPath(root);
        policy.EnsureFileAccess(fullRoot);
        lock (sync)
            return ValueTask.FromResult(snapshots.TryGetValue(fullRoot, out IReadOnlyList<FluNetFileIndexEntry>? snapshot)
                ? snapshot
                : Array.Empty<FluNetFileIndexEntry>());
    }

    public async ValueTask<IReadOnlyList<FluNetFileIndexEntry>> QueryAsync(string root, FluNetFileIndexQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        query.Validate();
        IReadOnlyList<FluNetFileIndexEntry> entries = await QueryAsync(root, cancellationToken).ConfigureAwait(false);
        return ApplyQuery(entries, query);
    }

    private static IReadOnlyList<FluNetFileIndexEntry> ApplyQuery(IReadOnlyList<FluNetFileIndexEntry> entries, FluNetFileIndexQuery query)
    {
        IEnumerable<FluNetFileIndexEntry> result = entries;
        if (!string.IsNullOrWhiteSpace(query.OrderBy))
        {
            string[] parts = query.OrderBy.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            bool descending = parts.Length > 1 && parts[1].Equals("DESC", StringComparison.OrdinalIgnoreCase);
            result = parts[0].ToLowerInvariant() switch
            {
                "name" => descending ? result.OrderByDescending(x => x.Name, StringComparer.OrdinalIgnoreCase) : result.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase),
                "extension" => descending ? result.OrderByDescending(x => x.Extension, StringComparer.OrdinalIgnoreCase) : result.OrderBy(x => x.Extension, StringComparer.OrdinalIgnoreCase),
                "length" or "size" => descending ? result.OrderByDescending(x => x.Length) : result.OrderBy(x => x.Length),
                "modifiedutc" or "modified" => descending ? result.OrderByDescending(x => x.ModifiedUtc) : result.OrderBy(x => x.ModifiedUtc),
                _ => throw new ArgumentException($"Unsupported index order field '{parts[0]}'.", nameof(query))
            };
        }
        return result.Skip(query.Skip).Take(query.Take ?? int.MaxValue).ToArray();
    }

    public async ValueTask<IReadOnlyList<FluNetFileIndexEntry>> ApplyChangeAsync(
        string root,
        FluNetFileChange change,
        bool recursive = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);
        string fullRoot = Path.GetFullPath(root);
        policy.EnsureFileAccess(fullRoot);
        bool requiresRebuild = false;
        lock (sync)
        {
            if (!snapshots.TryGetValue(fullRoot, out IReadOnlyList<FluNetFileIndexEntry>? current))
                requiresRebuild = true;
            if (change.IsDirectory == true)
                requiresRebuild = true;
            if (!requiresRebuild)
            {
                Dictionary<string, FluNetFileIndexEntry> entries = current!.ToDictionary(item => item.Path, StringComparer.OrdinalIgnoreCase);
                if (change.Kind == FluNetFileChangeKind.Deleted)
                    entries.Remove(Path.GetFullPath(change.Path));
                else if (change.Kind == FluNetFileChangeKind.Renamed && change.OldPath is not null)
                    entries.Remove(Path.GetFullPath(change.OldPath));
                if (change.Kind is not FluNetFileChangeKind.Deleted && File.Exists(change.Path))
                {
                    policy.EnsureFileAccess(change.Path);
                    FileInfo file = new(change.Path);
                    entries[file.FullName] = CreateEntry(file);
                }
                IReadOnlyList<FluNetFileIndexEntry> snapshot = entries.Values.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase).ToArray();
                snapshots[fullRoot] = snapshot;
                return snapshot;
            }
        }
        if (requiresRebuild)
            return await RebuildAsync(root, recursive, cancellationToken).ConfigureAwait(false);
        return [];
    }

    private static FluNetFileIndexEntry CreateEntry(FileInfo file) => new(
        file.FullName, file.Name, file.Extension, file.Length, file.LastWriteTimeUtc, file.CreationTimeUtc,
        file.Name.StartsWith(".", StringComparison.Ordinal) || file.Attributes.HasFlag(FileAttributes.Hidden), file.IsReadOnly);
}

/// <summary>Durable metadata index backed by a provider-neutral SQLite file.</summary>
public sealed class SqliteFluNetFileMetadataIndex : IFluNetFileMetadataIndex
{
    private readonly string path;
    private readonly IExecutionPolicy policy;
    private readonly SemaphoreSlim gate = new(1, 1);

    public SqliteFluNetFileMetadataIndex(string path, IExecutionPolicy policy)
    {
        this.path = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public async ValueTask<IReadOnlyList<FluNetFileIndexEntry>> RebuildAsync(string root, bool recursive = true, CancellationToken cancellationToken = default)
    {
        string fullRoot = Path.GetFullPath(root);
        policy.EnsureFileAccess(fullRoot);
        if (!Directory.Exists(fullRoot)) throw new DirectoryNotFoundException(fullRoot);
        EnumerationOptions options = new() { RecurseSubdirectories = recursive, IgnoreInaccessible = false, ReturnSpecialDirectories = false };
        List<FluNetFileIndexEntry> entries = [];
        foreach (string filePath in Directory.EnumerateFiles(fullRoot, "*", options).Order(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            policy.EnsureFileAccess(filePath);
            FileInfo file = new(filePath);
            entries.Add(new(file.FullName, file.Name, file.Extension, file.Length, file.LastWriteTimeUtc, file.CreationTimeUtc,
                file.Name.StartsWith(".", StringComparison.Ordinal) || file.Attributes.HasFlag(FileAttributes.Hidden), file.IsReadOnly));
        }
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using SqliteConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteCommand clear = connection.CreateCommand();
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM flunet_file_index WHERE root = $root;";
            clear.Parameters.AddWithValue("$root", fullRoot);
            await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            foreach (FluNetFileIndexEntry entry in entries)
            {
                await using SqliteCommand insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = "INSERT INTO flunet_file_index(root, path, name, extension, length, modified_utc, created_utc, is_hidden, is_read_only) VALUES ($root, $path, $name, $extension, $length, $modified, $created, $hidden, $readonly);";
                insert.Parameters.AddWithValue("$root", fullRoot); insert.Parameters.AddWithValue("$path", entry.Path);
                insert.Parameters.AddWithValue("$name", entry.Name); insert.Parameters.AddWithValue("$extension", entry.Extension);
                insert.Parameters.AddWithValue("$length", entry.Length); insert.Parameters.AddWithValue("$modified", entry.ModifiedUtc.ToString("O"));
                insert.Parameters.AddWithValue("$created", entry.CreatedUtc.ToString("O")); insert.Parameters.AddWithValue("$hidden", entry.IsHidden ? 1 : 0);
                insert.Parameters.AddWithValue("$readonly", entry.IsReadOnly ? 1 : 0);
                await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return entries;
        }
        finally { gate.Release(); }
    }

    public async ValueTask<IReadOnlyList<FluNetFileIndexEntry>> QueryAsync(string root, CancellationToken cancellationToken = default)
    {
        string fullRoot = Path.GetFullPath(root);
        policy.EnsureFileAccess(fullRoot);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using SqliteConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT path, name, extension, length, modified_utc, created_utc, is_hidden, is_read_only FROM flunet_file_index WHERE root = $root ORDER BY path;";
            command.Parameters.AddWithValue("$root", fullRoot);
            List<FluNetFileIndexEntry> entries = [];
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                entries.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt64(3),
                    DateTimeOffset.Parse(reader.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    DateTimeOffset.Parse(reader.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    reader.GetInt64(6) != 0, reader.GetInt64(7) != 0));
            return entries;
        }
        finally { gate.Release(); }
    }

    public async ValueTask<IReadOnlyList<FluNetFileIndexEntry>> QueryAsync(string root, FluNetFileIndexQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        query.Validate();
        string fullRoot = Path.GetFullPath(root);
        policy.EnsureFileAccess(fullRoot);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using SqliteConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand();
            List<string> predicates = ["root = $root"];
            command.Parameters.AddWithValue("$root", fullRoot);
            if (!string.IsNullOrWhiteSpace(query.Predicate))
                predicates.Add(AddPredicate(command, query.Predicate!));
            string order = TranslateOrder(query.OrderBy);
            command.CommandText = $"SELECT path, name, extension, length, modified_utc, created_utc, is_hidden, is_read_only FROM flunet_file_index WHERE {string.Join(" AND ", predicates)} ORDER BY {order} LIMIT $take OFFSET $skip;";
            command.Parameters.AddWithValue("$take", query.Take ?? int.MaxValue);
            command.Parameters.AddWithValue("$skip", query.Skip);
            return await ReadEntriesAsync(command, cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    private static string TranslateOrder(string? orderBy)
    {
        if (string.IsNullOrWhiteSpace(orderBy)) return "path ASC";
        string[] parts = orderBy.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string field = parts[0].ToLowerInvariant() switch
        {
            "path" => "path", "name" => "name", "extension" => "extension", "length" or "size" => "length",
            "modified" or "modifiedutc" => "modified_utc", "created" or "createdutc" => "created_utc",
            _ => throw new ArgumentException($"Unsupported index order field '{parts[0]}'.", nameof(orderBy))
        };
        string direction = parts.Length > 1 && parts[1].Equals("DESC", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        return $"{field} {direction}";
    }

    private static string AddPredicate(SqliteCommand command, string predicate)
    {
        List<string> terms = [];
        foreach (string rawTerm in System.Text.RegularExpressions.Regex.Split(predicate, @"\s+AND\s+", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            Match match = System.Text.RegularExpressions.Regex.Match(rawTerm.Trim(), "^(?<field>[A-Za-z][A-Za-z0-9_]*)\\s*(?<op>==|!=|>=|<=|>|<|CONTAINS|STARTS\\s+WITH|ENDS\\s+WITH)\\s*(?<value>.+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success) throw new ArgumentException($"Unsupported index predicate '{rawTerm}'.", nameof(predicate));
            string field = match.Groups["field"].Value.ToLowerInvariant() switch
            {
                "path" => "path", "name" => "name", "extension" => "extension", "length" or "size" => "length",
                "modified" or "modifiedutc" => "modified_utc", "created" or "createdutc" => "created_utc",
                "hidden" or "ishidden" => "is_hidden", "readonly" or "isreadonly" => "is_read_only",
                _ => throw new ArgumentException($"Unsupported index predicate field '{match.Groups["field"].Value}'.", nameof(predicate))
            };
            string op = match.Groups["op"].Value.ToUpperInvariant();
            string parameter = "$p" + command.Parameters.Count;
            string value = match.Groups["value"].Value.Trim().Trim('"', '\'');
            if (op is "CONTAINS" or "STARTS WITH" or "ENDS WITH")
            {
                string pattern = op switch { "CONTAINS" => $"%{value}%", "STARTS WITH" => $"{value}%", _ => $"%{value}" };
                command.Parameters.AddWithValue(parameter, pattern);
                terms.Add($"{field} LIKE {parameter}");
            }
            else
            {
                object parameterValue = field is "is_hidden" or "is_read_only"
                    ? (value.Equals("true", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                    : field == "length" && long.TryParse(value, out long length) ? length : value;
                command.Parameters.AddWithValue(parameter, parameterValue);
                terms.Add($"{field} {op switch { "==" => "=", "!=" => "<>", _ => op }} {parameter}");
            }
        }
        return $"({string.Join(" AND ", terms)})";
    }

    public async ValueTask<IReadOnlyList<FluNetFileIndexEntry>> ApplyChangeAsync(
        string root,
        FluNetFileChange change,
        bool recursive = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);
        string fullRoot = Path.GetFullPath(root);
        policy.EnsureFileAccess(fullRoot);
        if (change.IsDirectory == true)
            return await RebuildAsync(root, recursive, cancellationToken).ConfigureAwait(false);

        string changedPath = Path.GetFullPath(change.Path);
        EnsureWithinRoot(fullRoot, changedPath);
        if (change.Kind == FluNetFileChangeKind.Renamed && change.OldPath is not null)
            EnsureWithinRoot(fullRoot, Path.GetFullPath(change.OldPath));

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using SqliteConnection connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            if (change.Kind == FluNetFileChangeKind.Deleted || change.Kind == FluNetFileChangeKind.Renamed)
            {
                await using SqliteCommand delete = connection.CreateCommand();
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM flunet_file_index WHERE root = $root AND path = $path;";
                delete.Parameters.AddWithValue("$root", fullRoot);
                delete.Parameters.AddWithValue("$path", change.Kind == FluNetFileChangeKind.Renamed && change.OldPath is not null
                    ? Path.GetFullPath(change.OldPath) : changedPath);
                await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            if (change.Kind is FluNetFileChangeKind.Created or FluNetFileChangeKind.Changed or FluNetFileChangeKind.Renamed)
            {
                if (File.Exists(changedPath))
                {
                    policy.EnsureFileAccess(changedPath);
                    FileInfo file = new(changedPath);
                    await UpsertEntryAsync(connection, transaction, fullRoot, CreateEntry(file), cancellationToken).ConfigureAwait(false);
                }
            }
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
        return await QueryAsync(root, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<IReadOnlyList<FluNetFileIndexEntry>> ReadEntriesAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        List<FluNetFileIndexEntry> entries = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            entries.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt64(3),
                DateTimeOffset.Parse(reader.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind),
                DateTimeOffset.Parse(reader.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind), reader.GetInt64(6) != 0, reader.GetInt64(7) != 0));
        return entries;
    }

    private static async ValueTask UpsertEntryAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string root,
        FluNetFileIndexEntry entry,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO flunet_file_index(root, path, name, extension, length, modified_utc, created_utc, is_hidden, is_read_only)
            VALUES ($root, $path, $name, $extension, $length, $modified, $created, $hidden, $readonly)
            ON CONFLICT(root, path) DO UPDATE SET name=excluded.name, extension=excluded.extension, length=excluded.length,
                modified_utc=excluded.modified_utc, created_utc=excluded.created_utc, is_hidden=excluded.is_hidden, is_read_only=excluded.is_read_only;
            """;
        command.Parameters.AddWithValue("$root", root); command.Parameters.AddWithValue("$path", entry.Path);
        command.Parameters.AddWithValue("$name", entry.Name); command.Parameters.AddWithValue("$extension", entry.Extension);
        command.Parameters.AddWithValue("$length", entry.Length); command.Parameters.AddWithValue("$modified", entry.ModifiedUtc.ToString("O"));
        command.Parameters.AddWithValue("$created", entry.CreatedUtc.ToString("O")); command.Parameters.AddWithValue("$hidden", entry.IsHidden ? 1 : 0);
        command.Parameters.AddWithValue("$readonly", entry.IsReadOnly ? 1 : 0);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static FluNetFileIndexEntry CreateEntry(FileInfo file) => new(
        file.FullName, file.Name, file.Extension, file.Length, file.LastWriteTimeUtc, file.CreationTimeUtc,
        file.Name.StartsWith(".", StringComparison.Ordinal) || file.Attributes.HasFlag(FileAttributes.Hidden), file.IsReadOnly);

    private static void EnsureWithinRoot(string root, string path)
    {
        string relative = Path.GetRelativePath(root, path);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new CapabilityDeniedException($"Index change escapes the configured root: {path}");
    }

    private async ValueTask<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        policy.EnsureFileAccess(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory());
        SqliteConnection connection = new(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand schema = connection.CreateCommand();
        schema.CommandText = "CREATE TABLE IF NOT EXISTS flunet_file_index (root TEXT NOT NULL, path TEXT NOT NULL, name TEXT NOT NULL, extension TEXT NOT NULL, length INTEGER NOT NULL, modified_utc TEXT NOT NULL, created_utc TEXT NOT NULL, is_hidden INTEGER NOT NULL, is_read_only INTEGER NOT NULL, PRIMARY KEY(root, path)); CREATE INDEX IF NOT EXISTS ix_flunet_file_index_root_path ON flunet_file_index(root, path);";
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}

/// <summary>
/// Remote metadata-index provider. The endpoint contract is intentionally
/// small: POST /rebuild accepts a rebuild request and GET /query returns an
/// array (or an object containing <c>entries</c>) of index entries.
/// </summary>
public sealed class HttpFluNetFileMetadataIndex : IFluNetFileMetadataIndex
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Uri endpoint;
    private readonly IHttpTransport transport;
    private readonly IAuthenticatedHttpTransport? authenticated;
    private readonly SecretValue? credential;

    public HttpFluNetFileMetadataIndex(
        Uri endpoint,
        IHttpTransport transport,
        IAuthenticatedHttpTransport? authenticated = null,
        SecretValue? credential = null)
    {
        if (endpoint is null || !endpoint.IsAbsoluteUri || endpoint.Scheme is not ("http" or "https"))
            throw new ArgumentException("Index endpoint must be an absolute HTTP(S) URI.", nameof(endpoint));
        this.endpoint = endpoint.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? endpoint : new Uri(endpoint.AbsoluteUri + "/", UriKind.Absolute);
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.authenticated = authenticated;
        this.credential = credential;
        if (credential is not null && authenticated is null)
            throw new ArgumentException("An authenticated transport is required when an index credential is supplied.", nameof(authenticated));
    }

    public async ValueTask<IReadOnlyList<FluNetFileIndexEntry>> RebuildAsync(string root, bool recursive = true, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        string json = JsonSerializer.Serialize(new { root, recursive }, JsonOptions);
        string response = credential is null
            ? await transport.PostJsonAsync(new Uri(endpoint, "rebuild"), json, cancellationToken).ConfigureAwait(false)
            : await authenticated!.PostJsonAsync(new Uri(endpoint, "rebuild"), json, credential, cancellationToken).ConfigureAwait(false);
        return DeserializeEntries(response);
    }

    public ValueTask<IReadOnlyList<FluNetFileIndexEntry>> QueryAsync(string root, CancellationToken cancellationToken = default) =>
        QueryAsync(root, new FluNetFileIndexQuery(), cancellationToken);

    public async ValueTask<IReadOnlyList<FluNetFileIndexEntry>> QueryAsync(string root, FluNetFileIndexQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(query);
        query.Validate();
        UriBuilder builder = new(new Uri(endpoint, "query"));
        List<string> parameters = ["root=" + Uri.EscapeDataString(root)];
        if (!string.IsNullOrWhiteSpace(query.Predicate)) parameters.Add("predicate=" + Uri.EscapeDataString(query.Predicate));
        if (!string.IsNullOrWhiteSpace(query.OrderBy)) parameters.Add("orderBy=" + Uri.EscapeDataString(query.OrderBy));
        parameters.Add("skip=" + query.Skip.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (query.Take is int take) parameters.Add("take=" + take.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.Query = string.Join('&', parameters);
        HttpResourceResponse response = credential is null
            ? await transport.GetAsync(builder.Uri, cancellationToken).ConfigureAwait(false)
            : await authenticated!.GetAsync(builder.Uri, credential, cancellationToken).ConfigureAwait(false);
        return DeserializeEntries(Encoding.UTF8.GetString(response.Content));
    }

    private static IReadOnlyList<FluNetFileIndexEntry> DeserializeEntries(string json)
    {
        using JsonDocument document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
        JsonElement value = document.RootElement;
        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("entries", out JsonElement entries)) value = entries;
        if (value.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Remote metadata index response must be an array or an object with an 'entries' array.");
        return value.Deserialize<FluNetFileIndexEntry[]>(JsonOptions) ?? [];
    }
}

public sealed class FileMetadataIndexCapabilityProvider : ICapabilityProvider
{
    public CapabilityDescriptor Descriptor { get; } = new(
        "filesystem.metadata-index",
        "1.0",
        [FluNetPlatform.Any],
        ["filesystem.read"]);

    public bool IsAvailable => true;
}
