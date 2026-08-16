using FluNET.Language;
using FluNET.Language.Metadata;

namespace FluNET.Tests;

public class LanguageMetadataTests
{
    [Fact]
    public void Type_shape_distinguishes_scalar_from_collection_value()
    {
        TypeShape scalar = TypeShape.Analyze(typeof(FileInfo));
        TypeShape array = TypeShape.Analyze(typeof(FileInfo[]));

        Assert.False(scalar.IsCollection);
        Assert.True(array.IsCollection);
        Assert.Equal(typeof(FileInfo), array.ElementType);
    }

    [Fact]
    public void Constructor_metadata_uses_roles_and_params_for_syntactic_cardinality()
    {
        var compiler = new LanguageCompiler();

        ConstructorDescriptor constructor = Assert.Single(compiler.DescribeConstructors(typeof(ReflectionFixture)));
        ParameterDescriptor what = constructor.Parameters[0];
        ParameterDescriptor from = constructor.Parameters[1];

        Assert.Equal(ClauseKind.What, what.Role);
        Assert.False(what.IsParams);
        Assert.Equal(ClauseKind.From, from.Role);
        Assert.True(from.IsParams);
        Assert.True(from.Shape.IsCollection);
        Assert.Equal(typeof(FileInfo), from.Shape.ElementType);
    }

    private sealed class ReflectionFixture
    {
        public ReflectionFixture([What] string what, [From] params FileInfo[] from)
        {
        }
    }
}
