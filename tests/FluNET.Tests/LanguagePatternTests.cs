using FluNET.Language;
using FluNET.Language.Metadata;
using FluNET.Syntax.Core;

namespace FluNET.Tests;

public class LanguagePatternTests
{
    [Test]
    public void Compiler_creates_distinct_sentence_patterns_from_role_constructors()
    {
        var compiler = new LanguageCompiler();
        VerbDescriptor descriptor = compiler.DescribeVerb(typeof(MultiPatternVerb), "CUSTOM", [], () => null);
        Assert.That(descriptor.Patterns.Count, Is.EqualTo(2));
        Assert.That(descriptor.Patterns.Any(x => x.Pattern.Clauses.Count == 1), Is.True);
        Assert.That(descriptor.Patterns.Any(x => x.Pattern.Clauses.Count == 2), Is.True);
    }

    [Verb("CUSTOM")]
    private sealed class MultiPatternVerb : IVerb<string>
    {
        public MultiPatternVerb([What] string what) { }
        public MultiPatternVerb([What] string what, [From] FileInfo from) { }
        public string Text => "CUSTOM";
        public IWord? Next { get; set; }
        public IWord? Previous { get; set; }
        public bool Validate(IWord word) => true;
        public FluNET.Syntax.Validation.ValidationResult ValidateNext(IWord nextWord, FluNET.Lexicon.Lexicon lexicon) => FluNET.Syntax.Validation.ValidationResult.Success();
    }
}
