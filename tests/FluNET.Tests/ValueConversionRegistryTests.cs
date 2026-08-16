using FluNET.Binding;

namespace FluNET.Tests;

public class ValueConversionRegistryTests
{
    [Fact]
    public void Numeric_conversion_has_higher_cost_than_exact_match()
    {
        var conversions = new ValueConversionRegistry();
        Assert.True(conversions.TryGet(typeof(int), typeof(int), out ValueConversion? exact));
        Assert.True(conversions.TryGet(typeof(int), typeof(long), out ValueConversion? numeric));
        Assert.Equal(0, exact!.Cost);
        Assert.True(numeric!.Cost > exact.Cost);
        Assert.Equal(42L, numeric.Apply(42));
    }
}
