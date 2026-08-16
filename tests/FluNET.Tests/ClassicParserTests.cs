using FluNET.Language;
using FluNET.Syntax.Ast;
using FluNET.Syntax.Parsing;

namespace FluNET.Tests;

public class ClassicParserTests
{
    [Test]
    public void Parser_preserves_classic_sentence_shape_and_then_pipeline()
    {
        var parser = new ClassicParser(new LanguageRegistry().Snapshot);
        ParseResult result = parser.Parse("GET TEXT [data] FROM {input.txt} THEN SAY [data]");
        Assert.That(result.Success, Is.True);
        PipelineNode pipeline = result.Script!.Pipelines.Single();
        Assert.That(pipeline.Sentences.Count, Is.EqualTo(2));
        Assert.That(pipeline.Sentences[0].Verb, Is.EqualTo("GET"));
        Assert.That(pipeline.Sentences[0].Qualifier, Is.EqualTo("TEXT"));
        Assert.That(pipeline.Sentences[0].Clauses.Any(x => x.Kind == ClauseKind.What && x.Value is VariableExpression), Is.True);
        Assert.That(pipeline.Sentences[0].Clauses.Any(x => x.Kind == ClauseKind.From && x.Value is ReferenceExpression), Is.True);
    }

    [Test]
    public void Parser_keeps_multiple_values_for_the_same_role()
    {
        var parser = new ClassicParser(new LanguageRegistry().Snapshot);
        ParseResult result = parser.Parse("GET [data] FROM a.txt b.txt c.txt");
        SentenceNode sentence = result.Script!.Pipelines.Single().Sentences.Single();
        Assert.That(sentence.Clauses.Count(x => x.Kind == ClauseKind.From), Is.EqualTo(3));
    }
}
