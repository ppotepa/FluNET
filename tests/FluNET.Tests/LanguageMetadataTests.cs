using FluNET.Language;
using FluNET.Language.Metadata;

namespace FluNET.Tests;

public class LanguageMetadataTests
{
    [Test]
    public void Type_shape_distinguishes_scalar_from_collection_value()
    {
        TypeShape scalar = TypeShape.Analyze(typeof(FileInfo));
        TypeShape array = TypeShape.Analyze(typeof(FileInfo[]));
        Assert.That(scalar.IsCollection, Is.False);
        Assert.That(array.IsCollection, Is.True);
        Assert.That(array.ElementType, Is.EqualTo(typeof(FileInfo)));
    }

    [Test]
    public void Constructor_metadata_uses_roles_and_params_for_syntactic_cardinality()
    {
        var compiler = new LanguageCompiler();
        ConstructorDescriptor constructor = compiler.DescribeConstructors(typeof(ReflectionFixture)).Single();
        ParameterDescriptor what = constructor.Parameters[0];
        ParameterDescriptor from = constructor.Parameters[1];
        Assert.That(what.Role, Is.EqualTo(ClauseKind.What));
        Assert.That(what.IsParams, Is.False);
        Assert.That(from.Role, Is.EqualTo(ClauseKind.From));
        Assert.That(from.IsParams, Is.True);
        Assert.That(from.Shape.IsCollection, Is.True);
        Assert.That(from.Shape.ElementType, Is.EqualTo(typeof(FileInfo)));
    }

    private sealed class ReflectionFixture
    {
        public ReflectionFixture([What] string what, [From] params FileInfo[] from) { }
    }
}
