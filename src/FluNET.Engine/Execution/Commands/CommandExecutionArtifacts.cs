using FluNET.Language;
using FluNET.Language.Binding;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace FluNET.Execution.Commands;

public sealed record ExecutionCachePolicy(TimeSpan Ttl);

public static class CommandExecutionArtifactStore
{
    private sealed class Holder { public ExecutionCachePolicy? Cache { get; set; } }
    private static readonly ConditionalWeakTable<BoundCommand, Holder> Values = new();

    public static void SetCache(BoundCommand command, ExecutionCachePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(policy);
        Values.GetOrCreateValue(command).Cache = policy;
    }
    public static bool TryGetCache(BoundCommand command, out ExecutionCachePolicy? policy)
    {
        if (Values.TryGetValue(command, out Holder? holder) && holder.Cache is not null) { policy = holder.Cache; return true; }
        policy = null; return false;
    }

    public static string CacheKey(BoundCommand command)
    {
        string input = string.Join("|", command.Arguments.Values
            .Where(argument => argument.Slot.Direction == SlotDirection.Input)
            .OrderBy(argument => argument.RoleId.Value, StringComparer.Ordinal)
            .Select(argument => $"{argument.RoleId.Value}:{string.Join(" ", argument.Tokens.Select(token => token.Text))}"));
        string canonical = $"{command.Frame.Id.Value}|{input}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}

public interface IExecutionResultCache
{
    bool TryGet(string key, out object? value);
    void Set(string key, object? value, TimeSpan ttl);
    void Clear();
}

public sealed class InMemoryExecutionResultCache : IExecutionResultCache
{
    private sealed record Entry(object? Value, DateTimeOffset ExpiresAt);
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public bool TryGet(string key, out object? value)
    {
        if (_entries.TryGetValue(key, out Entry? entry))
        {
            if (entry.ExpiresAt > DateTimeOffset.UtcNow) { value = entry.Value; return true; }
            _entries.TryRemove(key, out _);
        }
        value = null; return false;
    }
    public void Set(string key, object? value, TimeSpan ttl)
    {
        if (ttl <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ttl));
        _entries[key] = new Entry(value, DateTimeOffset.UtcNow.Add(ttl));
    }
    public void Clear() => _entries.Clear();
}
