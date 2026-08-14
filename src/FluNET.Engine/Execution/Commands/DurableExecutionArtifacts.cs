using FluNET.Capabilities;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;

namespace FluNET.Execution.Commands;

public sealed record DurableExecutionArtifactsOptions(string Directory);

internal sealed record DurableValueEnvelope(bool IsNull, string? TypeName, string? Json)
{
    public static DurableValueEnvelope FromValue(object? value)
    {
        if (value is null) return new(true, null, null);
        Type type = value.GetType();
        string json = value switch
        {
            FileInfo file => JsonSerializer.Serialize(file.FullName),
            DirectoryInfo directory => JsonSerializer.Serialize(directory.FullName),
            Uri uri => JsonSerializer.Serialize(uri.AbsoluteUri),
            _ => JsonSerializer.Serialize(value, type)
        };
        return new(false, type.AssemblyQualifiedName, json);
    }

    public object? ToValue()
    {
        if (IsNull) return null;
        if (TypeName is null || Json is null) throw new InvalidDataException("Durable value envelope is incomplete.");
        Type type = Type.GetType(TypeName, throwOnError: false)
            ?? throw new InvalidDataException($"Durable value type '{TypeName}' is not available in this host.");
        if (type == typeof(FileInfo)) return new FileInfo(JsonSerializer.Deserialize<string>(Json)!);
        if (type == typeof(DirectoryInfo)) return new DirectoryInfo(JsonSerializer.Deserialize<string>(Json)!);
        if (type == typeof(Uri)) return new Uri(JsonSerializer.Deserialize<string>(Json)!, UriKind.Absolute);
        return JsonSerializer.Deserialize(Json, type);
    }
}

internal static class DurableArtifactFile
{
    public static Dictionary<string, T> Read<T>(string path, IExecutionPolicy policy)
    {
        policy.EnsureFileAccess(path);
        if (!File.Exists(path)) return new Dictionary<string, T>(StringComparer.Ordinal);
        string json = File.ReadAllText(path);
        Dictionary<string, T>? values = JsonSerializer.Deserialize<Dictionary<string, T>>(json);
        return values is null
            ? new Dictionary<string, T>(StringComparer.Ordinal)
            : new Dictionary<string, T>(values, StringComparer.Ordinal);
    }

    public static void Write<T>(string path, IReadOnlyDictionary<string, T> values, IExecutionPolicy policy)
    {
        policy.EnsureFileAccess(path);
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        policy.EnsureFileAccess(temp);
        byte[] content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(values));
        try
        {
            using (FileStream stream = new(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 16 * 1024, FileOptions.WriteThrough))
            {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}

public sealed class DurableExecutionResultCache : IExecutionResultCache
{
    private sealed record Entry(DurableValueEnvelope Value, DateTimeOffset ExpiresAt);
    private readonly string _path;
    private readonly IExecutionPolicy _policy;
    private readonly object _gate = new();

    public DurableExecutionResultCache(DurableExecutionArtifactsOptions options, IExecutionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(options);
        _path = Path.Combine(Path.GetFullPath(options.Directory), "execution-cache.json");
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public bool TryGet(string key, out object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        lock (_gate)
        {
            Dictionary<string, Entry> entries = DurableArtifactFile.Read<Entry>(_path, _policy);
            if (!entries.TryGetValue(key, out Entry? entry)) { value = null; return false; }
            if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                entries.Remove(key);
                DurableArtifactFile.Write(_path, entries, _policy);
                value = null;
                return false;
            }
            value = entry.Value.ToValue();
            return true;
        }
    }

    public void Set(string key, object? value, TimeSpan ttl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (ttl <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ttl));
        lock (_gate)
        {
            Dictionary<string, Entry> entries = DurableArtifactFile.Read<Entry>(_path, _policy);
            entries[key] = new Entry(DurableValueEnvelope.FromValue(value), DateTimeOffset.UtcNow.Add(ttl));
            DurableArtifactFile.Write(_path, entries, _policy);
        }
    }

    public void Clear()
    {
        lock (_gate) DurableArtifactFile.Write(_path, new Dictionary<string, Entry>(), _policy);
    }
}

public sealed class DurableIdempotencyStore : IIdempotencyStore
{
    private readonly string _path;
    private readonly IExecutionPolicy _policy;
    private readonly object _gate = new();

    public DurableIdempotencyStore(DurableExecutionArtifactsOptions options, IExecutionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(options);
        _path = Path.Combine(Path.GetFullPath(options.Directory), "idempotency.json");
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public bool TryGet(string key, out object? result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        lock (_gate)
        {
            Dictionary<string, DurableValueEnvelope> entries = DurableArtifactFile.Read<DurableValueEnvelope>(_path, _policy);
            if (!entries.TryGetValue(key, out DurableValueEnvelope? value)) { result = null; return false; }
            result = value.ToValue();
            return true;
        }
    }

    public void Record(string key, object? result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        lock (_gate)
        {
            Dictionary<string, DurableValueEnvelope> entries = DurableArtifactFile.Read<DurableValueEnvelope>(_path, _policy);
            if (entries.ContainsKey(key)) return;
            entries[key] = DurableValueEnvelope.FromValue(result);
            DurableArtifactFile.Write(_path, entries, _policy);
        }
    }

    public void Clear()
    {
        lock (_gate) DurableArtifactFile.Write(_path, new Dictionary<string, DurableValueEnvelope>(), _policy);
    }
}

public static class DurableExecutionArtifactExtensions
{
    public static IServiceCollection AddDurableExecutionArtifacts(this IServiceCollection services, string directory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        services.AddSingleton(new DurableExecutionArtifactsOptions(directory));
        services.AddSingleton<IExecutionResultCache, DurableExecutionResultCache>();
        services.AddSingleton<IIdempotencyStore, DurableIdempotencyStore>();
        return services;
    }
}
