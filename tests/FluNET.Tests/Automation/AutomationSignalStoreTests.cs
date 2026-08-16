using FluNET.Automation;
using FluNET.Capabilities;

namespace FluNET.Tests.Automation;

[TestFixture]
public sealed class AutomationSignalStoreTests
{
    [Test]
    public async Task InMemoryStorePreservesSignalOrderAndPayload()
    {
        InMemoryAutomationSignalStore store = new();
        AutomationSignal first = new("files", "CREATED", new Dictionary<string, object?> { ["path"] = "a.txt" });
        AutomationSignal second = new("files", "CHANGED", new Dictionary<string, object?> { ["path"] = "b.txt" });

        await store.AppendAsync(new AutomationSignalEnvelope(DateTimeOffset.UtcNow, first));
        await store.AppendAsync(new AutomationSignalEnvelope(DateTimeOffset.UtcNow, second));
        IReadOnlyList<AutomationSignalEnvelope> signals = await store.ReadAsync();

        Assert.That(signals.Select(item => item.Signal.EventName), Is.EqualTo(new[] { "CREATED", "CHANGED" }));
        Assert.That(signals[0].Signal.Data["path"], Is.EqualTo("a.txt"));
    }

    [Test]
    public async Task JsonFileStorePersistsSignalsAcrossInstances()
    {
        string directory = Path.Combine(Path.GetTempPath(), "flunet-signals-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "signals.jsonl");
        try
        {
            AutomationSignal signal = new("files", "CREATED", new Dictionary<string, object?>
            {
                ["path"] = "a.txt",
                ["length"] = 12L
            });
            JsonFileAutomationSignalStore writer = new(path, new AllowAllExecutionPolicy());
            await writer.AppendAsync(new AutomationSignalEnvelope(DateTimeOffset.UtcNow, signal));

            JsonFileAutomationSignalStore reader = new(path, new AllowAllExecutionPolicy());
            IReadOnlyList<AutomationSignalEnvelope> signals = await reader.ReadAsync();

            Assert.That(signals, Has.Count.EqualTo(1));
            Assert.That(signals[0].Signal.Resource, Is.EqualTo("files"));
            Assert.That(signals[0].Signal.Data["path"]?.ToString(), Is.EqualTo("a.txt"));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task SqliteStorePersistsSignalsAcrossInstances()
    {
        string directory = Path.Combine(Path.GetTempPath(), "flunet-sqlite-signals-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "signals.db");
        try
        {
            AutomationSignal signal = new("files", "CHANGED", new Dictionary<string, object?>
            {
                ["path"] = "a.txt",
                ["length"] = 24L
            });
            SqliteAutomationSignalStore writer = new(path, new AllowAllExecutionPolicy());
            await writer.AppendAsync(new AutomationSignalEnvelope(DateTimeOffset.UtcNow, signal));

            SqliteAutomationSignalStore reader = new(path, new AllowAllExecutionPolicy());
            IReadOnlyList<AutomationSignalEnvelope> signals = await reader.ReadAsync();

            Assert.That(signals, Has.Count.EqualTo(1));
            Assert.That(signals[0].Signal.EventName, Is.EqualTo("CHANGED"));
            Assert.That(signals[0].Signal.Data["path"]?.ToString(), Is.EqualTo("a.txt"));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
