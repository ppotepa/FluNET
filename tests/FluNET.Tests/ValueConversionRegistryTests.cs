using FluNET.Binding;

namespace FluNET.Tests;

public class ValueConversionRegistryTests
{
    [Test]
    public void Numeric_conversion_has_higher_cost_than_exact_match()
    {
        var conversions = new ValueConversionRegistry();
        Assert.That(conversions.TryGet(typeof(int), typeof(int), out ValueConversion? exact), Is.True);
        Assert.That(conversions.TryGet(typeof(int), typeof(long), out ValueConversion? numeric), Is.True);
        Assert.That(exact, Is.Not.Null);
        Assert.That(numeric, Is.Not.Null);
        Assert.That(exact!.Cost, Is.EqualTo(0));
        Assert.That(numeric!.Cost, Is.GreaterThan(exact.Cost));
        Assert.That(numeric.Apply(42), Is.EqualTo(42L));
    }
}
