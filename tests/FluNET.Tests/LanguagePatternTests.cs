using FluNET.Language;
using FluNET.Language.Metadata;
using FluNET.Syntax.Core;

namespace FluNET.Tests;

public class LanguagePatternTests
{
    [Fact]
    public void Compiler_creates_distinct_sentence_patterns_from_role_constructors()
    {
        var compiler = new LanguageCompiler();
        VerbDescriptor descriptor = compiler.DescribeVerb(
            typeof(MultiPatternVerb),
            "CUSTOM",
            [],
            () => null);

        Assert.Equal(2, descriptor.Patterns.Count);
        Assert.Contains(descriptor.Patterns, x => x.Pattern.Clauses.Count == 1);
        Assert.Contains(descriptor.Patterns, x => x.Pattern.Clauses.Count == 2);
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
