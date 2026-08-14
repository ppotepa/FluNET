using FluNET.Language;
using FluNET.Language.Binding;
using FluNET.Variables;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FluNET.Execution.Commands;

public sealed record ExecutionCachePolicy(TimeSpan Ttl);
public sealed record ExecutionIdempotencyPolicy(string KeyExpression);

public static class CommandExecutionArtifactStore
{
    private sealed class Holder { public ExecutionCachePolicy? Cache { get; set; } public ExecutionIdempotencyPolicy? Idempotency { get; set; } }
    private static readonly ConditionalWeakTable<BoundCommand, Holder> Values = new();
    public static void SetCache(BoundCommand command, ExecutionCachePolicy policy) => Values.GetOrCreateValue(command).Cache = policy;
    public static bool TryGetCache(BoundCommand command, out ExecutionCachePolicy? policy) { if (Values.TryGetValue(command, out Holder? holder) && holder.Cache is not null) { policy = holder.Cache; return true; } policy = null; return false; }
    public static void SetIdempotency(BoundCommand command, ExecutionIdempotencyPolicy policy) => Values.GetOrCreateValue(command).Idempotency = policy;
    public static bool TryGetIdempotency(BoundCommand command, out ExecutionIdempotencyPolicy? policy) { if (Values.TryGetValue(command, out Holder? holder) && holder.Idempotency is not null) { policy = holder.Idempotency; return true; } policy = null; return false; }

    public static string CommandFingerprint(BoundCommand command)
    {
        string input = string.Join("|", command.Arguments.Values
            .Where(argument => argument.Slot.Direction == SlotDirection.Input)
            .OrderBy(argument => argument.RoleId.Value, StringComparer.Ordinal)
            .Select(argument => $"{argument.RoleId.Value}:{string.Join(" ", argument.Tokens.Select(token => token.Text))}"));
        return Hash($"{command.Frame.Id.Value}|{input}");
    }

    public static string IdempotencyKey(BoundCommand command, ExecutionIdempotencyPolicy policy, IVariableResolver variables)
    {
        object? value;
        if (DynamicPathExpression.TryParse(policy.KeyExpression, out DynamicPathExpression? path)) value = path!.Evaluate(variables);
        else { string text = policy.KeyExpression.Trim(); value = text.Length >= 2 && ((text[0] == '"' && text[^1] == '"') || (text[0] == '\'' && text[^1] == '\'')) ? text[1..^1] : text; }
        return Hash($"{CommandFingerprint(command)}|{JsonSerializer.Serialize(value)}");
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public interface IExecutionResultCache { bool TryGet(string key, out object? value); void Set(string key, object? value, TimeSpan ttl); void Clear(); }
public sealed class InMemoryExecutionResultCache : IExecutionResultCache
{
    private sealed record Entry(object? Value, DateTimeOffset ExpiresAt); private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    public bool TryGet(string key, out object? value) { if (_entries.TryGetValue(key, out Entry? entry)) { if (entry.ExpiresAt > DateTimeOffset.UtcNow) { value = entry.Value; return true; } _entries.TryRemove(key, out _); } value = null; return false; }
    public void Set(string key, object? value, TimeSpan ttl) { if (ttl <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ttl)); _entries[key] = new Entry(value, DateTimeOffset.UtcNow.Add(ttl)); }
    public void Clear() => _entries.Clear();
}

public interface IIdempotencyStore { bool TryGet(string key, out object? result); void Record(string key, object? result); void Clear(); }
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, object> _entries = new(StringComparer.Ordinal); private sealed class NullValue { public static NullValue Instance { get; } = new(); }
    public bool TryGet(string key, out object? result) { if (_entries.TryGetValue(key, out object? value)) { result = ReferenceEquals(value, NullValue.Instance) ? null : value; return true; } result = null; return false; }
    public void Record(string key, object? result) => _entries.TryAdd(key, result ?? NullValue.Instance);
    public void Clear() => _entries.Clear();
}
