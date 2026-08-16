using FluNET.Capabilities;
using NUnit.Framework;

namespace FluNET.Tests.Capabilities;

[TestFixture]
public sealed class KeyValueStorageTests
{
    [Test]
    public async Task JsonFileStoreSurvivesAProviderRecreation()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-storage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "state.json");
        try
        {
            JsonFileFluNetKeyValueStore first = new(path, new AllowAllExecutionPolicy());
            await first.SetAsync("theme", "dark");

            JsonFileFluNetKeyValueStore second = new(path, new AllowAllExecutionPolicy());

            Assert.That(await second.GetAsync("theme"), Is.EqualTo("dark"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task JsonFileStoreLeavesOnlyTheCommittedSnapshot()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-storage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "state.json");
        try
        {
            JsonFileFluNetKeyValueStore store = new(path, new AllowAllExecutionPolicy());
            await store.SetAsync("one", "1");
            await store.SetAsync("two", "2");

            Assert.That(File.Exists(path), Is.True);
            Assert.That(Directory.EnumerateFiles(root, "*.tmp"), Is.Empty);
            Assert.That(await store.GetAsync("one"), Is.EqualTo("1"));
            Assert.That(await store.GetAsync("two"), Is.EqualTo("2"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task InMemoryStoreListsValuesByPrefixInStableOrder()
    {
        InMemoryFluNetKeyValueStore store = new();
        await store.SetAsync("user:two", "2");
        await store.SetAsync("user:one", "1");
        await store.SetAsync("other", "x");

        IReadOnlyList<KeyValuePair<string, string>> values = await store.ListAsync("user:");

        Assert.That(values.Select(pair => pair.Key), Is.EqualTo(new[] { "user:one", "user:two" }));
        Assert.That(values.Select(pair => pair.Value), Is.EqualTo(new[] { "1", "2" }));
    }

    [Test]
    public async Task SqliteStoreSurvivesProviderRecreationAndSupportsDelete()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-storage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "state.db");
        try
        {
            SqliteFluNetKeyValueStore first = new(path, new AllowAllExecutionPolicy());
            await first.SetAsync("user:one", "1");
            await first.SetAsync("user:two", "2");

            SqliteFluNetKeyValueStore second = new(path, new AllowAllExecutionPolicy());
            Assert.That((await second.ListAsync("user:")).Select(pair => pair.Key), Is.EqualTo(new[] { "user:one", "user:two" }));
            Assert.That(await second.DeleteAsync("user:one"), Is.True);
            Assert.That(await second.GetAsync("user:one"), Is.Null);
            Assert.That(await second.DeleteAsync("user:one"), Is.False);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
