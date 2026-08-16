using FluNET.Capabilities;

namespace FluNET.Tests.Capabilities;

[TestFixture]
public sealed class MessagingTests
{
    [Test]
    public async Task JsonFileBusPersistsAndConsumesMessages()
    {
        string path = Path.Combine(Path.GetTempPath(), "flunet-messages-" + Guid.NewGuid().ToString("N") + ".jsonl");
        try
        {
            JsonFileFluNetMessageBus bus = new(path, new AllowAllExecutionPolicy());
            await bus.PublishAsync("jobs", "backup");

            FluNetMessage message = await bus.ReceiveAsync("jobs");

            Assert.That(message.Topic, Is.EqualTo("jobs"));
            Assert.That(message.Payload, Is.EqualTo("backup"));
            Assert.That(File.ReadAllText(path), Does.Not.Contain("backup"));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
