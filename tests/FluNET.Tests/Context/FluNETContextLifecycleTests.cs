using FluNET.Context;

namespace FluNET.Tests.Context;

[TestFixture]
public sealed class FluNETContextLifecycleTests
{
    [Test]
    public async Task DefaultContextIsCreatedOnceAcrossConcurrentReaders()
    {
        FluNETContext.ResetDefault();
        try
        {
            Task<FluNETContext>[] readers = Enumerable.Range(0, 32)
                .Select(_ => Task.Run(() => FluNETContext.Default))
                .ToArray();

            FluNETContext[] contexts = await Task.WhenAll(readers);

            Assert.That(contexts.Distinct().Count(), Is.EqualTo(1));
        }
        finally
        {
            FluNETContext.ResetDefault();
        }
    }

    [Test]
    public void ContextBuildValidatesDefaultDependencyGraph()
    {
        Assert.DoesNotThrow(() =>
        {
            using FluNETContext context = FluNETContext.Create();
            _ = context.GetEngine();
        });
    }
}
