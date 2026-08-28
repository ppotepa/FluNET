using FluNET.Context;

namespace FluNET.Tests.Context;

[TestFixture]
public sealed class FluNETContextLifecycleTests
{
    [Test]
    public async Task ConcurrentCreationProducesIndependentOwnedContexts()
    {
        Task<FluNETContext>[] creators = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(FluNETContext.Create))
            .ToArray();

        FluNETContext[] contexts = await Task.WhenAll(creators);
        try
        {
            Assert.That(contexts.Distinct().Count(), Is.EqualTo(contexts.Length));
            Assert.That(contexts.All(context => context.GetEngine() is not null), Is.True);
        }
        finally
        {
            foreach (FluNETContext context in contexts)
                context.Dispose();
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

    [Test]
    public async Task AsyncDisposalClosesTheOwnedServiceProvider()
    {
        FluNETContext context = FluNETContext.Create();
        _ = context.GetEngine();

        await context.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => context.GetEngine());
    }
}
