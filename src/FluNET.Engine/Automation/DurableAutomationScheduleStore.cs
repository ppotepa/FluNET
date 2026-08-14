using FluNET.Capabilities;
using System.Text.Json;

namespace FluNET.Automation;

/// <summary>Durable single-host timer state. Compiled definitions are registered by the host after restart.</summary>
public sealed class DurableAutomationScheduleStore : IAutomationScheduleStore
{
    private readonly string _path;
    private readonly IExecutionPolicy _policy;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public DurableAutomationScheduleStore(string path, IExecutionPolicy policy)
    { _path = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path))); _policy = policy ?? throw new ArgumentNullException(nameof(policy)); }

    public async ValueTask<AutomationScheduleState?> GetAsync(string automationId, CancellationToken cancellationToken = default)
    {
        Dictionary<string, AutomationScheduleState> states = await ReadAsync(cancellationToken).ConfigureAwait(false);
        return states.TryGetValue(automationId, out AutomationScheduleState? state) ? state : null;
    }

    public async ValueTask SetAsync(AutomationScheduleState state, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Dictionary<string, AutomationScheduleState> states = await ReadUnlockedAsync(cancellationToken).ConfigureAwait(false);
            states[state.AutomationId] = state;
            await WriteUnlockedAsync(states, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private async Task<Dictionary<string, AutomationScheduleState>> ReadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await ReadUnlockedAsync(cancellationToken).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    private async Task<Dictionary<string, AutomationScheduleState>> ReadUnlockedAsync(CancellationToken cancellationToken)
    {
        _policy.EnsureFileAccess(_path);
        if (!File.Exists(_path)) return new(StringComparer.OrdinalIgnoreCase);
        string json = await File.ReadAllTextAsync(_path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<Dictionary<string, AutomationScheduleState>>(json)
            ?? new Dictionary<string, AutomationScheduleState>(StringComparer.OrdinalIgnoreCase);
    }

    private async Task WriteUnlockedAsync(Dictionary<string, AutomationScheduleState> states, CancellationToken cancellationToken)
    {
        _policy.EnsureFileAccess(_path);
        string? directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        string temp = _path + ".tmp";
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(states));
        await using (FileStream stream = new(temp, FileMode.Create, FileAccess.Write, FileShare.None, 16 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            stream.Flush(true);
        }
        File.Move(temp, _path, overwrite: true);
    }
}
