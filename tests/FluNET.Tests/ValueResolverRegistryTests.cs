using FluNET.Binding;

namespace FluNET.Tests;

public class ValueResolverRegistryTests
{
    [Test]
    public void Reflection_fallback_resolves_enum_and_string_constructor_types()
    {
        var resolvers = new ValueResolverRegistry();
        Assert.That(resolvers.TryResolve("Friday", typeof(DayOfWeek), out object? day), Is.True);
        Assert.That(day, Is.EqualTo(DayOfWeek.Friday));
        Assert.That(resolvers.TryResolve("alpha", typeof(StringConstructed), out object? custom), Is.True);
        Assert.That(custom, Is.TypeOf<StringConstructed>());
        Assert.That(((StringConstructed)custom!).Value, Is.EqualTo("alpha"));
    }

    [Test]
    public void Repeated_values_resolve_to_array_shape()
    {
        var resolvers = new ValueResolverRegistry();
        var context = new ResolutionContext(typeof(FileInfo[]));
        Assert.That(resolvers.TryResolveMany(["a.txt", "b.txt"], typeof(FileInfo[]), context, out object? result), Is.True);
        Assert.That(result, Is.TypeOf<FileInfo[]>());
        Assert.That(((FileInfo[])result!).Length, Is.EqualTo(2));
    }

    private sealed class StringConstructed
    {
        public StringConstructed(string value) => Value = value;
        public string Value { get; }
    }
}
