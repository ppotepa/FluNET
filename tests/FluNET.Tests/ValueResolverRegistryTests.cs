using FluNET.Binding;

namespace FluNET.Tests;

public class ValueResolverRegistryTests
{
    [Fact]
    public void Reflection_fallback_resolves_enum_and_string_constructor_types()
    {
        var resolvers = new ValueResolverRegistry();

        Assert.True(resolvers.TryResolve("Friday", typeof(DayOfWeek), out object? day));
        Assert.Equal(DayOfWeek.Friday, day);

        Assert.True(resolvers.TryResolve("alpha", typeof(StringConstructed), out object? custom));
        Assert.Equal("alpha", Assert.IsType<StringConstructed>(custom).Value);
    }

    [Fact]
    public void Repeated_values_resolve_to_array_shape()
    {
        var resolvers = new ValueResolverRegistry();
        var context = new ResolutionContext(typeof(FileInfo[]));

        Assert.True(resolvers.TryResolveMany(["a.txt", "b.txt"], typeof(FileInfo[]), context, out object? result));
        FileInfo[] files = Assert.IsType<FileInfo[]>(result);
        Assert.Equal(2, files.Length);
    }

    private sealed class StringConstructed
    {
        public StringConstructed(string value) => Value = value;
        public string Value { get; }
    }
}
