using FluNET.Capabilities;

namespace FluNET.Tests.Capabilities;

[TestFixture]
public sealed class SqliteMessagingTests
{
    [Test]
    public async Task SqliteBusPersistsAndConsumesMessagesInTopicOrder()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-messages-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "messages.db");
        try
        {
            SqliteFluNetMessageBus first = new(path, new AllowAllExecutionPolicy());
            await first.PublishAsync("orders", "one");
            await first.PublishAsync("orders", "two");
            await first.PublishAsync("other", "ignored");

            SqliteFluNetMessageBus second = new(path, new AllowAllExecutionPolicy());
            FluNetMessage one = await second.ReceiveAsync("orders");
            FluNetMessage two = await second.ReceiveAsync("orders");

            Assert.Multiple(() =>
            {
                Assert.That(one.Payload, Is.EqualTo("one"));
                Assert.That(two.Payload, Is.EqualTo("two"));
                Assert.That(one.MessageId, Is.Not.EqualTo(two.MessageId));
            });
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
