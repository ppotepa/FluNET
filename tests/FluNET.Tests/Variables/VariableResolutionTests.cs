using FluNET.Context;
using FluNET.Variables;

namespace FluNET.Tests.Variables;

[TestFixture]
public sealed class VariableResolutionTests
{
    [Test]
    public void TryResolveDistinguishesRegisteredDefaultValueFromMissingVariable()
    {
        using FluNETContext context = FluNETContext.Create();
        IVariableResolver resolver = context.GetService<IVariableResolver>();
        resolver.Register("zero", 0);

        bool found = resolver.TryResolve<int>("[zero]", out int value);
        bool missing = resolver.TryResolve<int>("[missing]", out _);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(value, Is.EqualTo(0));
            Assert.That(missing, Is.False);
        });
    }

    [Test]
    public void PersistentResolverReportsMalformedStructuredValueAsFailure()
    {
        PersistentVariableResolver resolver = new();
        resolver.Clear();

        bool resolved = resolver.TryResolve<Dictionary<string, string>>("[{not-json}]", out _);

        Assert.That(resolved, Is.False);
    }
}
