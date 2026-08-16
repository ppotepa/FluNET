using FluNET.Compilation;
using FluNET.Language;

namespace FluNET.Tests;

public class ClassicCompilerTests
{
    [Fact]
    public void Compiler_binds_a_classic_get_from_source_text()
    {
        LanguageSnapshot language = new LanguageRegistry().Snapshot;
        var compiler = new ClassicCompiler(language);

        ClassicCompilation result = compiler.Compile("GET [text] FROM {input.txt}");

        Assert.True(result.Success);
        Assert.Single(result.Pipelines);
        Assert.Equal(typeof(string[]), result.Pipelines[0].ResultType);
    }
}
