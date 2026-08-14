using FluNET.Capabilities;
using FluNET.Declarative.Reconciliation;
using System.Text.Json;

namespace FluNET.Tests.Declarative;

[TestFixture]
public sealed class ReconciliationDurableIntegrityTests
{
    [Test]
    public async Task CorruptBaselineIsRejectedInsteadOfSilentlyAccepted()
    {
        string directory = TempDirectory("baseline");
        try
        {
            DurableReconciliationStateStore store = new(
                new DurableReconciliationStateOptions(directory),
                new AllowAllExecutionPolicy());
            await store.SetAsync(new(
                "sync-corrupt",
                "file",
                Path.GetFullPath("target.json"),
                "id",
                [JsonSerializer.SerializeToElement(new { id = 1, name = "Ada" })],
                DateTimeOffset.UtcNow));
            string path = Directory.EnumerateFiles(directory, "*.reconciliation.json").Single();
            string content = await File.ReadAllTextAsync(path);
            await File.WriteAllTextAsync(path, content.Replace("Ada", "Eve", StringComparison.Ordinal));

            Assert.ThrowsAsync<InvalidDataException>(async () => await store.GetAsync("sync-corrupt"));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    public async Task CorruptCheckpointJournalIsRejectedInsteadOfBeingUsedForRecovery()
    {
        string directory = TempDirectory("checkpoint");
        try
        {
            DurableReconciliationCheckpointStore store = new(
                new DurableReconciliationCheckpointOptions(directory),
                new AllowAllExecutionPolicy());
            await store.AppendAsync(new(
                Guid.NewGuid(),
                "sync-corrupt",
                ReconciliationCheckpointPhase.Applying,
                DateTimeOffset.UtcNow,
                41,
                Creates: 1));
            string path = Directory.EnumerateFiles(directory, "*.checkpoint.jsonl").Single();
            string content = await File.ReadAllTextAsync(path);
            int marker = content.IndexOf("Applying", StringComparison.Ordinal);
            Assert.That(marker, Is.GreaterThanOrEqualTo(0));
            string corrupted = content[..marker] + "AppliedX" + content[(marker + "Applying".Length)..];
            await File.WriteAllTextAsync(path, corrupted);

            Assert.ThrowsAsync<InvalidDataException>(async () => await store.ReadAsync("sync-corrupt"));
        }
        finally { Directory.Delete(directory, true); }
    }

    private static string TempDirectory(string suffix)
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"FluNET_Reconciliation_{suffix}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
