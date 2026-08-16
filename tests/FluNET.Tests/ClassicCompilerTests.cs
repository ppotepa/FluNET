using FluNET.Compilation;
using FluNET.Language;

namespace FluNET.Tests;

public class ClassicCompilerTests
{
    [Test]
    public void Compiler_binds_a_classic_get_from_source_text()
    {
        LanguageSnapshot language = new LanguageRegistry().Snapshot;
        var compiler = new ClassicCompiler(language);
        ClassicCompilation result = compiler.Compile("GET [text] FROM {input.txt}");
        Assert.That(result.Success, Is.True);
        Assert.That(result.Pipelines.Count, Is.EqualTo(1));
        Assert.That(result.Pipelines[0].ResultType, Is.EqualTo(typeof(string[])));
    }
}
